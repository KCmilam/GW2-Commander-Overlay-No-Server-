
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Awesomium.Windows.Controls
Imports System.Net.Sockets
Imports System.Text
Imports Awesomium.Core
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.ComponentModel
Imports PieControls
Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.IO.MemoryMappedFiles

Public Class MainWindow
    Dim jsObject As JSObject

    Dim MapInfo As MapData
    Dim MapInfoCreated As Boolean = False

    Dim PlayerName As String = ""
    Dim PlayerRotation As Integer = 0
    Dim PlayerStatus As Char = "n"
    Dim AllPlayers As String = "@"
    Dim Map As WebControl
    Dim ClientSocket As New TcpClient
    Dim ServerStream As NetworkStream
    'Dim ServerAddress As String = "192.168.101.190"
    Dim PortNumber As Integer = 11000       ' Set the port number used by the server

    Dim Commanders As String = "~"
    Dim CommanderMMF As MemoryMappedFile

    Delegate Sub delError()
    Delegate Sub delUpdateplayers(ByVal Players As String)
    Delegate Sub delCallJavascript(Name As String, Params As JSValue)
    Delegate Sub delUpdateScores(Scores As List(Of MapScore))

    Dim ErrorShown As Boolean = False
    Dim TickCount As Integer = 7
    Dim TimerStart As Integer = 0
    Dim RunUpdateMap As Boolean = True
    Public UpdateInterval As Integer = 800

    Dim UpdateThread As Thread
    Dim APIThread As Thread
    Dim PlayerListThread As New Thread(AddressOf UpdatePlayerList)
    Dim OnlinePlayers As String = ""
    Dim ScoreMode As String = "Overall"

    Private WithEvents kbHook As New KeyBinder.KeyboardHook

    Dim PortalLaid As Boolean = False
    Dim MousePos As New Point(0, 0)
    Dim lastUItick As Integer = -1
    Dim playeractive As Boolean = False

    Dim link As MumbleLink
    Dim IsTopMost As Boolean = False
    Dim WaitingResponse = False
    Public ShowingAdmin As Boolean = False

    Dim MapTimers As ServerTest.LocalMapTimers

    Public Structure PlayerInfo
        Public Name As String
        Public Commander As Boolean
    End Structure

    <DllImport("USER32.DLL", EntryPoint:="GetActiveWindow", SetLastError:=True, CharSet:=CharSet.Unicode, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function GetActiveWindowHandle() As System.IntPtr
    End Function

    <DllImport("USER32.DLL", EntryPoint:="GetWindowText", SetLastError:=True, CharSet:=CharSet.Unicode, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function GetActiveWindowText(ByVal hWnd As System.IntPtr, ByVal lpString As System.Text.StringBuilder, ByVal cch As Integer) As Integer
    End Function

    Declare Function GetForegroundWindow Lib "user32" Alias "GetForegroundWindow" () As Long

    Private Sub kbHood_KeyCombinationPressed(sender As Object, e As KeyBinder.KeyboardHook.KeyboardHookEventArgs)
        If e.KeyPressed = "L" Then
            Dim ActiveWindowCaption As New System.Text.StringBuilder(256)
            Dim hWnd As Long = GetForegroundWindow()
            GetActiveWindowText(hWnd, ActiveWindowCaption, ActiveWindowCaption.Capacity)
            Dim ActiveWindow As String = ActiveWindowCaption.ToString
            If ActiveWindow = "Guild Wars 2" Or ActiveWindow = "GW2 Commander" Then
                IsTopMost = Not IsTopMost
                Me.Topmost = IsTopMost

                If Not IsTopMost Then
                    AppActivate("GW2 Commander")
                    Me.Visibility = Windows.Visibility.Hidden
                    AppActivate("Guild Wars 2")
                Else
                    Me.Visibility = Windows.Visibility.Visible
                End If
            End If
        ElseIf e.KeyPressed = "K" Then
            If Not PortalLaid Then
                Dispatcher.Invoke(New delCallJavascript(AddressOf CallJavascript), New Object() {"CreateRangeMarkerOnPlayer", New JSValue(5000)})
                PortalLaid = True
            Else
                Dispatcher.Invoke(New delCallJavascript(AddressOf CallJavascript), New Object() {"RemoveRangeMarker", New JSValue(5000)})
                PortalLaid = False
            End If
        End If
    End Sub

    Private Sub Form1_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Loaded
        Dim mObjectives As New MapData
        MapData.SelectedServerID = My.Settings.Server

        Me.Height = My.Settings.Height
        Me.Width = My.Settings.Width
        Me.Left = My.Settings.PositionX
        Me.Top = My.Settings.PositionY

        cvsScores.Visibility = Windows.Visibility.Hidden
        btnToggleScores.Margin = New Thickness(0, 0, 0, 0)

        pnlOptions.Width = 0
        spRanges.Visibility = Windows.Visibility.Hidden
        chkKeepOnTop.Visibility = Windows.Visibility.Hidden

        Me.Opacity = sliderTransparency.Value * 0.1
        AddHandler kbHook.KeyCombinationPressed, AddressOf kbHood_KeyCombinationPressed
        kbHook.SelectedKey = Key.L
        kbHook.SelectedKey2 = Key.K


        cbStatus.Items.Add("Normal")
        cbStatus.Items.Add("Havoc")
        cbStatus.Items.Add("Scout")
        cbStatus.SelectedIndex = 0

        MapTimers = New ServerTest.LocalMapTimers
        MapTimers.Start(MapData.SelectedServerID)

        UpdatePlayers("")
    End Sub

    Private Sub Main_FormClosing(sender As Object, e As System.EventArgs) Handles Me.Closing
        My.Settings.Height = Me.Height
        My.Settings.Width = Me.Width
        My.Settings.PositionX = Me.Left
        My.Settings.PositionY = Me.Top
        My.Settings.Save()

        RunUpdateMap = False
        MapTimers.KillThread()
    End Sub

    Private Sub lblEP_Click(sender As Object, e As System.EventArgs) Handles lblEP.MouseLeftButtonUp
        If My.Computer.Keyboard.CtrlKeyDown And My.Computer.Keyboard.AltKeyDown Then
            If InputBox("Password") = "disco" Then
                Dim AdminForm As New frmAdmin(Me, ClientSocket)
                'AdminForm.Location = New Point(Me.Location.X + Me.Width / 2 - AdminForm.Width / 2, Me.Location.Y + Me.Height / 2 - AdminForm.Height / 2)
                AdminForm.Show()
            End If
        End If
    End Sub

    Public Sub InitializeMap()
        Dim docText As String = My.Computer.FileSystem.ReadAllText(AppDomain.CurrentDomain.BaseDirectory & "Map.htm")

        docText = docText.Replace("CHARTMARKERSHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Markers/leaflet.dvf.chartmarkers.js")
        docText = docText.Replace("JQUERYSCRIPTHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "jquery.js")
        docText = docText.Replace("MAPJSHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "map.js")
        docText = docText.Replace("PLAYERICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/icon-player-n.png")
        docText = docText.Replace("PLAYERHAVOCICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/icon-player-h.png")
        docText = docText.Replace("PLAYERSCOUTICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/icon-player-s.png")
        docText = docText.Replace("PLAYERMEICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/icon-meplayer.png")
        docText = docText.Replace("PLAYERREMOVEDICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/icon-player-removed.png")
        docText = docText.Replace("CAMPBLUEICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/camp_blue.png")
        docText = docText.Replace("CAMPGREENICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/camp_green.png")
        docText = docText.Replace("CAMPREDICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/camp_red.png")
        docText = docText.Replace("CAMPNEUTRALICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/camp_neutral.png")
        docText = docText.Replace("TOWERBLUEICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/tower_blue.png")
        docText = docText.Replace("TOWERGREENICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/tower_green.png")
        docText = docText.Replace("TOWERREDICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/tower_red.png")
        docText = docText.Replace("TOWERNEUTRALICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/tower_neutral.png")
        docText = docText.Replace("CASTLEBLUEICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/castle_blue.png")
        docText = docText.Replace("CASTLEGREENICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/castle_green.png")
        docText = docText.Replace("CASTLEREDICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/castle_red.png")
        docText = docText.Replace("CASTLENEUTRALICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/castle_neutral.png")
        docText = docText.Replace("WAYPOINTICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/waypoint.png")
        docText = docText.Replace("COMMANDERICONHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/commander.png")

        'Ruin Icons
        docText = docText.Replace("HOLLOWBLUEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BattlesHollowBlue.png")
        docText = docText.Replace("HOLLOWREDHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BattlesHollowRed.png")
        docText = docText.Replace("HOLLOWGREENHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BattlesHollowGreen.png")
        docText = docText.Replace("HOLLOWWHITEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BattlesHollowWhite.png")
        docText = docText.Replace("ESTATEBLUEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BauersEstateBlue.png")
        docText = docText.Replace("ESTATEREDHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BauersEstateRed.png")
        docText = docText.Replace("ESTATEGREENHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BauersEstateGreen.png")
        docText = docText.Replace("ESTATEWHITEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/BauersEstateWhite.png")
        docText = docText.Replace("ASCENTBLUEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/CarversAscentBlue.png")
        docText = docText.Replace("ASCENTREDHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/CarversAscentRed.png")
        docText = docText.Replace("ASCENTGREENHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/CarversAscentGreen.png")
        docText = docText.Replace("ASCENTWHITEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/CarversAscentWhite.png")
        docText = docText.Replace("OVERLOOKBLUEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/OrchardOverlookBlue.png")
        docText = docText.Replace("OVERLOOKREDHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/OrchardOverlookRed.png")
        docText = docText.Replace("OVERLOOKGREENHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/OrchardOverlookGreen.png")
        docText = docText.Replace("OVERLOOKWHITEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/OrchardOverlookWhite.png")
        docText = docText.Replace("TEMPLEBLUEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/TempleofLostPrayersBlue.png")
        docText = docText.Replace("TEMPLEREDHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/TempleofLostPrayersRed.png")
        docText = docText.Replace("TEMPLEGREENHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/TempleofLostPrayersGreen.png")
        docText = docText.Replace("TEMPLEWHITEHERE", "file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "Images/TempleofLostPrayersWhite.png")

        My.Computer.FileSystem.WriteAllText(AppDomain.CurrentDomain.BaseDirectory & "MyMap.htm", docText, False)

        Map = New WebControl
        Map.NavigationInfo = Awesomium.Core.NavigationInfo.Verbose
        AddHandler Map.ConsoleMessage, AddressOf BrowserMessage
        AddHandler Map.DocumentReady, AddressOf MapReady
        AddHandler Map.ShowContextMenu, AddressOf ShowContextMenu
        Grid.SetRow(Map, 1)
        MapGrid.Children.Add(Map)
        Map.Source = New Uri("file://" & AppDomain.CurrentDomain.BaseDirectory.Replace("\", "/") & "MyMap.htm")
    End Sub

    Private Sub MapReady()
        jsObject = Map.CreateGlobalJavascriptObject("jsobject")
        jsObject.Bind("ShowContextMenu", True, AddressOf JSRightClick)
        jsObject.Bind("StartUpdating", False, AddressOf StartUpdating)

        link = New MumbleLink

        UpdateThread = New Thread(AddressOf UpdateMap)
        UpdateThread.Start()
    End Sub

    Private Sub StartUpdating(ByVal sender As Object, ByVal e As JavascriptMethodEventArgs)
        UpdateThread = New Thread(AddressOf UpdateMap)
        UpdateThread.Start()

        APIThread = New Thread(AddressOf RunAPIThread)
        APIThread.Start()
    End Sub

    Private Sub ShowContextMenu(sender As Object, e As ContextMenuEventArgs)
        e.Handled = True
    End Sub

    'Private Function ConnectToServer() As Boolean
    '    If Not ClientSocket.Connected Then
    '        Try
    '            ClientSocket.ReceiveBufferSize = 8192
    '            ClientSocket.Connect(Serveraddress, PortNumber)
    '            Return True
    '        Catch
    '            Return False
    '        End Try
    '    Else
    '        Return True
    '    End If
    'End Function

    Public Sub CallJavascript(Name As String, Params As JSValue)
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync(Name, Params)
                End If
            End Using
        End If
    End Sub

    Public Sub UpdatePlayers(ByVal Players As String)
        AllPlayers = Players
        If Players = "" Then
            MapInfo = New MapData
            MapInfo.UpdateObjectives()
            lblGreenServer.Content = MapInfo.Match.GreenServerName
            lblBlueServer.Content = MapInfo.Match.BlueServerName
            lblRedServer.Content = MapInfo.Match.RedServerName
            InitializeMap()
        Else
            If Map.IsDocumentReady Then
                Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                    If window IsNot Nothing Then
                        If Not MapInfoCreated Then
                            Thread.Sleep(1000)
                            window.InvokeAsync("CreateMapInfoMarker", {MapInfo.Match.GreenServerName & "~-181~107.75|" & MapInfo.Match.RedServerName & "~-141.125~163.78125|" & MapInfo.Match.BlueServerName & "~-170.875~219.625"})
                            MapInfoCreated = True
                        Else
                            'window.InvokeAsync("UpdatePlayers", {ReplacePOInames(Players), PlayerName, PlayerRotation, Commanders})
                            window.InvokeAsync("UpdatePlayers", {Players, PlayerName, PlayerRotation, Commanders})
                            If Not PlayerListThread.IsAlive Then
                                OnlinePlayers = Players.Split("@")(0)
                                PlayerListThread = New Thread(AddressOf UpdatePlayerList)
                                PlayerListThread.Start()
                            End If
                        End If
                    End If
                End Using
            End If
        End If
    End Sub

    Private Sub UpdateMap()
        While RunUpdateMap
            If Not ShowingAdmin Then
                Dim lm As MumbleLink.LinkedMem = link.Read()
                Dim mPlayerInfo As PlayerInfo = GetPlayerInfo(lm.identity)
                PlayerName = mPlayerInfo.Name
                PlayerRotation = Math.Atan2(lm.fAvatarFront(2), lm.fAvatarFront(0)) * 180 / Math.PI
                If PlayerRotation < 0 Then
                    PlayerRotation = PlayerRotation + 360
                End If

                Me.Dispatcher.Invoke(New delUpdateplayers(AddressOf UpdatePlayers), New Object() {PlayerName & "~" & lm.fAvatarPosition(0) & "~" & lm.fAvatarPosition(2) & "~" & (lm.context(29) * 256 + lm.context(28)) & "~" & PlayerStatus & MapTimers.GetRetrieveString})

                'Me.Dispatcher.Invoke(New delUpdateplayers(AddressOf UpdatePlayers), New Object() {MapTimers.GetRetrieveString})
                'SendToServer("GW2UPDATE~" & PlayerName & "~" & lm.fAvatarPosition(0) & "~" & lm.fAvatarPosition(2) & "~" & (lm.context(29) * 256 + lm.context(28)) & "~" & PlayerStatus)
                playeractive = True
                lastUItick = lm.uiTick
                If lastUItick = 0 Then lastUItick = -1
            End If
            Thread.Sleep(UpdateInterval)
        End While
    End Sub

    Private Function GetPlayerInfo(PlayerIdentity As String) As PlayerInfo
        Dim mPlayerInfo As New PlayerInfo
        mPlayerInfo.Name = ""
        mPlayerInfo.Commander = False
        If PlayerIdentity <> "" Then
            Dim json As JObject = JObject.Parse(PlayerIdentity)

            For Each mChild As JProperty In json.Children.ToList
                If mChild.Name = "name" Then
                    mPlayerInfo.Name = mChild.Value.ToString
                ElseIf mChild.Name = "commander" Then
                    If mChild.Value.ToString.ToUpper = "FALSE" Then
                        mPlayerInfo.Commander = False
                    Else
                        mPlayerInfo.Commander = True
                    End If
                End If
            Next
        End If

        Return mPlayerInfo
    End Function

    Private Sub BrowserMessage(ByVal sender As Object, ByVal e As Awesomium.Core.ConsoleMessageEventArgs)
        MsgBox(e.Message)
    End Sub

    Private Sub ShowError()
        MessageBox.Show("An error has occurred trying to access the Mumble Link file.  You will be able to view other player's positions, but yours will not be available." & vbCrLf &
                        "To correct this, restart Guild Wars and Mumble while leaving this program open.")
    End Sub

    Private Sub btnOptions_Click(sender As System.Object, e As System.EventArgs) Handles btnOptions.Click
        If btnOptions.Content = ">" Then
            pnlOptions.Width = 150
            btnOptions.Content = "<"
        Else
            pnlOptions.Width = 0
            btnOptions.Content = ">"
        End If
    End Sub

    Private Sub chkCenterOnPlayer_Changed(sender As System.Object, e As System.EventArgs) Handles chkCenterOnPlayer.Checked
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("SetCenterOnPlayer", True)
                End If
            End Using
        End If
    End Sub

    Private Sub chkCenterOnPlayer_Unchecked(sender As System.Object, e As System.EventArgs) Handles chkCenterOnPlayer.Unchecked
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("SetCenterOnPlayer", False)
                End If
            End Using
        End If
    End Sub

    Private Sub lbOnline_DoubleClick(sender As Object, e As System.EventArgs) Handles lbOnline.MouseDoubleClick
        If lbOnline.SelectedItems.Count = 1 Then
            chkCenterOnPlayer.IsChecked = False
            If Map.IsDocumentReady Then
                Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                    If window IsNot Nothing Then
                        Dim center As Boolean = False
                        If chkCenterOnPlayer.IsChecked Then center = True
                        window.InvokeAsync("SetCenterOnPlayer", center)
                        window.InvokeAsync("panToPlayer", lbOnline.SelectedItems(0).ToString)
                    End If
                End Using
            End If
        End If
    End Sub

    Private Sub brnTrebRange_Click(ByVal sender As Object, ByVal e As System.Windows.RoutedEventArgs) Handles brnTrebRange.Click, btnCatapultRange.Click, btnBallistaRange.Click, btnMortarRange.Click, btnCannonRange.Click
        spRanges.Visibility = Windows.Visibility.Hidden
        Dispatcher.Invoke(New delCallJavascript(AddressOf CallJavascript), New Object() {"CreateRangeMarker", New JSValue(MousePos.X & "~" & MousePos.Y & "~" & GetSiegeRange(sender.content))})
    End Sub

    Public Function GetSiegeRange(ByVal Siege As String) As Integer
        Select Case Siege
            Case "Treb Range" : Return 10000
            Case "Mortar Range" : Return 9400
            Case "Catapult Range" : Return 4000
            Case "Ballista Range" : Return 3000
            Case "Cannon Range" : Return 3750
        End Select
        Return 0
    End Function

    Private Sub JSRightClick(ByVal sender As Object, ByVal e As JavascriptMethodEventArgs)
        If e.MustReturnValue Then
            e.Result = "Returning " & e.Arguments(0).ToString
            MousePos = New Point(e.Arguments(0).ToString.Split("~")(0), e.Arguments(0).ToString.Split("~")(1))
           spRanges.Margin = New Thickness(Mouse.GetPosition(MapGrid).X - 5, Mouse.GetPosition(MapGrid).Y - 5, 0, 0)
            spRanges.Visibility = Windows.Visibility.Visible
        End If
    End Sub

    Private Sub btnClearSiege_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnClearSiege.Click
        spRanges.Visibility = Windows.Visibility.Hidden
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("ClearSiegeMarkers")
                End If
            End Using
        End If
    End Sub

    Public Function GetPlayername() As String
        Dim mlink As New MumbleLink
        Dim lm As MumbleLink.LinkedMem = mlink.Read()
        Return lm.identity
    End Function

    Private Sub WindowBar_MouseLeftButtonDown(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles WindowBar.MouseLeftButtonDown
        Me.DragMove()
    End Sub

    Private Sub sliderTransparency_ValueChanged(ByVal sender As Object, ByVal e As System.Windows.RoutedPropertyChangedEventArgs(Of Double)) Handles sliderTransparency.ValueChanged
        Me.Opacity = sliderTransparency.Value * 0.1
    End Sub

    Private Sub btnClose_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub chkKeepOnTop_Checked(ByVal sender As Object, ByVal e As System.Windows.RoutedEventArgs) Handles chkKeepOnTop.Checked
        Me.Topmost = True
        pnlOptions.Width = 0
    End Sub

    Private Sub spRanges_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles spRanges.MouseLeave
        spRanges.Visibility = Windows.Visibility.Hidden
    End Sub

    Private Sub btnMinMaximize_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnMinMaximize.Click
        Select Case Me.WindowState
            Case Windows.WindowState.Maximized : Me.WindowState = Windows.WindowState.Normal
            Case Windows.WindowState.Normal : Me.WindowState = Windows.WindowState.Maximized
        End Select
    End Sub

    Private Sub btnMinimize_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnMinimize.Click
        Me.WindowState = Windows.WindowState.Minimized
    End Sub

    Private Sub cbStatus_SelectionChanged(sender As Object, e As System.Windows.Controls.SelectionChangedEventArgs) Handles cbStatus.SelectionChanged
        PlayerStatus = cbStatus.SelectedValue.Substring(0, 1).ToLower
    End Sub

    Private Sub btnSetCommander_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnSetCommander.Click
        If lbOnline.SelectedItem IsNot Nothing Then
            If Not Commanders.Contains(lbOnline.SelectedItem & "~") Then
                Commanders &= lbOnline.SelectedItem & "~"
            End If
        End If
    End Sub

    Private Sub btnRemoveCommander_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnRemoveCommander.Click
        If lbOnline.SelectedItem IsNot Nothing Then
            If Commanders.Contains(lbOnline.SelectedItem) Then
                Commanders = Commanders.Replace(lbOnline.SelectedItem & "~", "")
            End If
        End If
    End Sub

    Private Sub cmOnline_Opened(sender As Object, e As System.Windows.RoutedEventArgs) Handles cmOnline.Opened
        If lbOnline.SelectedItem IsNot Nothing Then
            Dim IsCommander As Boolean = False
            For i As Integer = 0 To Commanders.Split("~").Count - 1
                If Commanders.Split("~")(i) = lbOnline.SelectedItem Then
                    IsCommander = True
                    Exit For
                End If
            Next

            If IsCommander Then
                btnSetCommander.Visibility = Windows.Visibility.Collapsed
                btnRemoveCommander.Visibility = Windows.Visibility.Visible
            Else
                btnSetCommander.Visibility = Windows.Visibility.Visible
                btnRemoveCommander.Visibility = Windows.Visibility.Collapsed
            End If
        End If
    End Sub

    Private Sub ApplyZoom(sender As Object, e As MouseWheelEventArgs)
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("ZoomIn")
                End If
            End Using
        End If
    End Sub

#Region "API Thread"
    Dim apiTick As Integer = 9
    Private Sub RunAPIThread()
        While RunUpdateMap
            Try
                apiTick += 1
                If apiTick >= 10 Then
                    Dispatcher.Invoke(New delUpdateScores(AddressOf UpdateMapScores), New Object() {MatchData.GetMapScores(MapInfo.Match.ID)})
                    apiTick = 0
                End If
                Thread.Sleep(1000)
            Catch
            End Try
        End While
    End Sub

    Private Sub UpdateMapScores(Scores As List(Of MapScore))
        For Each mScore As MapScore In Scores
            Dim mPieData As New ObservableCollection(Of PieSegment)

            Dim blueSegment As New PieSegment
            blueSegment.Color = Colors.Blue
            blueSegment.Value = IIf(ScoreMode <> "Overall", mScore.BluePotential, mScore.BlueScore)
            blueSegment.Name = MapInfo.Match.BlueServerName
            mPieData.Add(blueSegment)

            Dim GreenSegment As New PieSegment
            GreenSegment.Color = Colors.Green
            GreenSegment.Value = IIf(ScoreMode <> "Overall", mScore.GreenPotential, mScore.GreenScore)
            GreenSegment.Name = MapInfo.Match.GreenServerName
            mPieData.Add(GreenSegment)

            Dim redSegment As New PieSegment
            redSegment.Color = Colors.Red
            redSegment.Value = IIf(ScoreMode <> "Overall", mScore.RedPotential, mScore.RedScore)
            redSegment.Name = MapInfo.Match.RedServerName
            mPieData.Add(redSegment)

            Select Case mScore.Map
                Case "RedHome" : pieRedServer.Data = mPieData
                Case "GreenHome" : pieGreenServer.Data = mPieData
                Case "BlueHome" : pieBlueServer.Data = mPieData
                Case "Center" : pieEBServer.Data = mPieData
                Case "Total" : pieTotal.Data = mPieData
            End Select
        Next
    End Sub

    Private Sub btnToggleScores_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnToggleScores.Click
        If btnToggleScores.Content = "v" Then
            cvsScores.Visibility = Windows.Visibility.Visible
            btnToggleScores.Margin = New Thickness(0, 75, 0, 0)
            btnToggleScores.Content = "^"
        Else
            cvsScores.Visibility = Windows.Visibility.Hidden
            btnToggleScores.Margin = New Thickness(0, 0, 0, 0)
            btnToggleScores.Content = "v"
        End If
    End Sub

    Private Sub btnOverallScore_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnOverallScore.Click
        ScoreMode = "Overall"
        btnOverallScore.Background = New SolidColorBrush(Colors.Gray)
        btnPotentialScore.Background = New SolidColorBrush(Colors.Black)
        Try
            UpdateMapScores(MatchData.GetMapScores(MapInfo.Match.ID))
        Catch
        End Try
    End Sub

    Private Sub btnPotentialScore_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnPotentialScore.Click
        ScoreMode = "Potential"
        btnPotentialScore.Background = New SolidColorBrush(Colors.Gray)
        btnOverallScore.Background = New SolidColorBrush(Colors.Black)
        Try
            UpdateMapScores(MatchData.GetMapScores(MapInfo.Match.ID))
        Catch
        End Try
    End Sub
#End Region

#Region "Online Player List"
    Delegate Sub delAddRemoveOnlinePlayer(ByVal PlayerName As String)

    Private Sub UpdatePlayerList()
        For Each mPlayer As String In OnlinePlayers.Split("|")
            Dim mPlayerName As String = mPlayer.Split("~")(0)
            If Not lbOnline.Items.Contains(mPlayerName) And mPlayerName.Length > 1 Then
                Dispatcher.Invoke(New delAddRemoveOnlinePlayer(AddressOf AddOnlinePlayer), New Object() {mPlayerName})
            End If
        Next

        For i As Integer = lbOnline.Items.Count - 1 To 0 Step -1
            If Not OnlinePlayers.Contains(lbOnline.Items(i)) Then
                Dispatcher.Invoke(New delAddRemoveOnlinePlayer(AddressOf RemoveOnlinePlayer), New Object() {lbOnline.Items(i)})
            End If
        Next
    End Sub

    Private Sub AddOnlinePlayer(ByVal PlayerName As String)
        Dim mItem As New ListBoxItem
        lbOnline.Items.Add(PlayerName)
    End Sub

    Private Sub RemoveOnlinePlayer(ByVal PlayerName As String)
        lbOnline.Items.Remove(PlayerName)
    End Sub
#End Region

#Region "Map Focusing"
    Private Sub btnFocusRedMap_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnFocusRedMap.Click
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("focusRedMap")
                End If
            End Using
            spRanges.Visibility = Windows.Visibility.Hidden
        End If
    End Sub

    Private Sub btnFocusGreenMap_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnFocusGreenMap.Click
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("focusGreenMap")
                End If
            End Using
            spRanges.Visibility = Windows.Visibility.Hidden
        End If
    End Sub

    Private Sub btnFocusBlueMap_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnFocusBlueMap.Click
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("focusBlueMap")
                End If
            End Using
            spRanges.Visibility = Windows.Visibility.Hidden
        End If
    End Sub

    Private Sub btnFocusEBMap_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnFocusEBMap.Click
        If Map.IsDocumentReady Then
            Using window As JSObject = Map.ExecuteJavascriptWithResult("window")
                If window IsNot Nothing Then
                    window.InvokeAsync("focusEBMap")
                End If
            End Using
            spRanges.Visibility = Windows.Visibility.Hidden
        End If
    End Sub
#End Region

#Region "Compass Support"
    Private Sub WriteCommanderPositionsToMemory()
        Dim mPlayers As String() = AllPlayers.Split("@")(0).Split("|")
        Dim mCommanderString As String = ""

        Monitor.Enter(Commanders)
        For c As Integer = 0 To Commanders.Split("~").Length - 1
            For i As Integer = 0 To mPlayers.Count - 1
                If mPlayers(i).Split("~")(0) <> "" AndAlso mPlayers(i).Split("~")(0) = Commanders.Split("~")(c) Then
                    mCommanderString &= mPlayers(i) & "|"
                End If
            Next
        Next
        Monitor.Exit(Commanders)

        Using accessor = CommanderMMF.CreateViewAccessor(0, 4096)
            If mCommanderString.Length > 3 Then
                Dim writeValue As Byte() = Encoding.GetEncoding("ISO-8859-1").GetBytes(mCommanderString)
                accessor.WriteArray(Of Byte)(0, writeValue, 0, writeValue.Length - 1)
            Else
                Dim writeValue As Byte() = Encoding.GetEncoding("ISO-8859-1").GetBytes("**NONE**")
                accessor.WriteArray(Of Byte)(0, writeValue, 0, writeValue.Length - 1)
            End If
        End Using
    End Sub
#End Region

    Private Sub btnServer_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnServer.Click
        Dim mSelectServer As New SelectServer
        mSelectServer.ShowDialog()
        MsgBox("Application restart is required for changes to be applied.")
    End Sub
End Class
