Imports System.Net.Sockets
Imports System.Text

Public Class frmAdmin
    Dim mMain As MainWindow
    Dim ClientSocket As TcpClient
    Dim ServerStream As NetworkStream

    Delegate Sub delGetWhitelist(Whitelist As String)

    Public Sub New(ByVal Main As MainWindow, ClientSocket As TcpClient)
        InitializeComponent()
        mMain = Main
        Main.ShowingAdmin = True
        Me.ClientSocket = ClientSocket

        If ClientSocket.Connected Then
            Dim inStream(10024) As Byte
            ServerStream = ClientSocket.GetStream()

            Dim outstream As Byte() = Encoding.GetEncoding("ISO-8859-1").GetBytes("GW2GETWHITELIST")

            ServerStream.Write(outstream, 0, outstream.Length)
            ServerStream.Flush()

            While True
                ServerStream.Read(inStream, 0, CInt(ClientSocket.ReceiveBufferSize))
                Dim ReceivedData As String = Encoding.GetEncoding("ISO-8859-1").GetString(inStream)

                If ReceivedData.StartsWith("GW2WHITELIST") Then
                    Dispatcher.Invoke(New delGetWhitelist(AddressOf PopulateWhitelist), New Object() {ReceivedData})
                    Exit While
                End If
            End While
        End If
    End Sub

    Private Sub btnClose_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub PopulateWhitelist(Whitelist As String)
        Dim SortedList As New List(Of String)
        For i As Integer = 1 To Whitelist.Split("~").Count - 1
            Dim mPlayer As String = Whitelist.Split("~")(i)
            If mPlayer.Length >= 3 And mPlayer.Length <= 19 Then
                SortedList.Add(mPlayer)
            End If
        Next

        SortedList.Sort(Function(x, y) x.Substring(0, 1).CompareTo(y.Substring(0, 1)))

        For Each mPlayer As String In SortedList
            lbWhitelist.Items.Add(mPlayer)
        Next
    End Sub

    Private Sub btnAdd_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnAdd.Click
        If txbAddPlayer.Text.Length >= 3 And txbAddPlayer.Text.Length <= 19 And Not txbAddPlayer.Text.Contains("~") Then
            If ClientSocket.Connected Then
                Dim inStream(10024) As Byte
                ServerStream = ClientSocket.GetStream()

                Dim outstream As Byte() = Encoding.GetEncoding("ISO-8859-1").GetBytes("GW2WHITELISTADD~" & txbAddPlayer.Text & "~")

                ServerStream.Write(outstream, 0, outstream.Length)
                ServerStream.Flush()
                lbWhitelist.Items.Add(txbAddPlayer.Text)
                txbAddPlayer.Text = ""
            End If
        Else
            MsgBox("Invalid character name")
        End If
    End Sub

    Private Sub lbWhitelist_KeyUp(sender As Object, e As System.Windows.Input.KeyEventArgs) Handles lbWhitelist.KeyUp
        If lbWhitelist.SelectedItem IsNot Nothing And e.Key = Key.Delete Then
            If lbWhitelist.SelectedItem = "Nogas" Then
                MsgBox("Nope")
            Else
                If MessageBox.Show("Are you sure you want to remove " & lbWhitelist.SelectedItem & " from the white list?", "Confirm Removal", MessageBoxButton.YesNo) = MessageBoxResult.Yes Then
                    If ClientSocket.Connected Then
                        Dim inStream(10024) As Byte
                        ServerStream = ClientSocket.GetStream()

                        Dim outstream As Byte() = Encoding.GetEncoding("ISO-8859-1").GetBytes("GW2WHITELISTREMOVE~" & lbWhitelist.SelectedItem & "~")

                        ServerStream.Write(outstream, 0, outstream.Length)
                        ServerStream.Flush()

                        lbWhitelist.Items.Remove(lbWhitelist.SelectedItem)
                    End If
                End If
            End If

        End If
    End Sub

    Private Sub frmAdmin_Closing(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles Me.Closing
        mMain.ShowingAdmin = False
    End Sub
End Class