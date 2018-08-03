
Imports Newtonsoft.Json.Linq

Public Class Objective
    Public Map As String
    Public ID As String
    Public Owner As String
    Public Name As String
    Public FullName As String
    Public ObjectiveType As String
    Public Update As Boolean = False
    Public OwnerChangeTime As Date
    Public Points As Integer

    Public Shared ObjectiveNamesJSON As String = ""
    Private Shared mObjectiveNames As List(Of ObjectiveName)

    Public Shared ReadOnly Property ObjectiveNames As List(Of ObjectiveName)
        Get
            If mObjectiveNames Is Nothing Then
                mObjectiveNames = New List(Of ObjectiveName)
                Try
                    'Dim json As JObject = JObject.Parse(My.Computer.FileSystem.ReadAllText(AppDomain.CurrentDomain.BaseDirectory & "objectivenames.txt"))
                    'For Each mChild As JProperty In json.Children.ToList
                    '    For Each mObj As JToken In mChild.Value.Children.ToList
                    '        Dim mObjectiveName As New ObjectiveName
                    '        For Each mProp As JProperty In mObj.Children.ToList
                    '            If mProp.Name = "id" Then
                    '                mObjectiveName.ID = mProp.Value
                    '            ElseIf mProp.Name = "name" Then
                    '                mObjectiveName.Name = mProp.Value
                    '            ElseIf mProp.Name = "full_name" Then
                    '                mObjectiveName.FullName = mProp.Value
                    '            ElseIf mProp.Name = "type" Then
                    '                mObjectiveName.ObjectiveType = mProp.Value
                    '            ElseIf mProp.Name = "points" Then
                    '                mObjectiveName.Points = mProp.Value
                    '            ElseIf mProp.Name = "map" Then
                    '                mObjectiveName.Map = mProp.Value
                    '            End If
                    '        Next
                    '        mObjectiveNames.Add(mObjectiveName)
                    '    Next
                    'Next

                    Dim ObjectiveJson As String = JSONUtility.GetJsonText("https://api.guildwars2.com/v2/wvw/objectives?ids=all")
                    ObjectiveJson = "{""objectives"":" & ObjectiveJson & "}"
                    Dim json As JObject = JObject.Parse(ObjectiveJson)
                    For Each mChild As JProperty In json.Children.ToList
                        For Each mObj As JToken In mChild.Value.Children.ToList
                            Dim mObjectiveName As New ObjectiveName
                            For Each mProp As JProperty In mObj.Children.ToList
                                If mProp.Name = "id" Then
                                    mObjectiveName.ID = mProp.Value.ToString.Split("-")(1)
                                ElseIf mProp.Name = "name" Then
                                    mObjectiveName.Name = mProp.Value.ToString
                                    mObjectiveName.FullName = mProp.Value.ToString
                                    If mObjectiveName.Name.Contains(" ") Then
                                        mObjectiveName.Name = mObjectiveName.Name.Split(" ")(0)
                                        mObjectiveName.FullName = mObjectiveName.Name.Split(" ")(0)
                                    End If
                                ElseIf mProp.Name = "type" Then
                                    mObjectiveName.ObjectiveType = mProp.Value
                                ElseIf mProp.Name = "points" Then
                                    mObjectiveName.Points = mProp.Value
                                ElseIf mProp.Name = "map_type" Then
                                    mObjectiveName.Map = mProp.Value
                                    'If mObjectiveName.Map = "C" Then
                                    '    mObjectiveName.Map = "E"
                                    'ElseIf mProp.Value.ToString = "EdgeOfTheMists" Then
                                    '    mObjectiveName.Map = "Edge"
                                    'End If

                                End If
                            Next
                            If mObjectiveName.Map <> "EdgeOfTheMists" Then mObjectiveNames.Add(mObjectiveName)
                        Next
                    Next
                Catch
                End Try
            End If
            Return mObjectiveNames
        End Get
    End Property

    Public Shared Function GetObjectiveData(ByVal MatchID As String) As List(Of Objective)
        Dim mObjectives As New List(Of Objective)
        MapData.MapScores.Clear()
        Dim json As JObject = JObject.Parse(JSONUtility.GetJsonText("https://api.guildwars2.com/v1/wvw/match_details.json?match_id=" & MatchID))

        For Each mChild As JProperty In json.Children.ToList
            If mChild.Name = "maps" Then
                For Each mMap As JToken In mChild.Value.Children.ToList
                    Dim MapName As String = ""

                    For Each mSubMap As JProperty In mMap.Children.ToList
                        If mSubMap.Name = "type" Then
                            MapName = mSubMap.Value
                        ElseIf mSubMap.Name = "objectives" Then
                            For Each mObjective As JToken In mSubMap.Value.ToList
                                Dim NewObjective As New Objective
                                NewObjective.Map = MapName
                                For Each mObj As JProperty In mObjective.Children.ToList
                                    If mObj.Name = "id" Then
                                        NewObjective.ID = mObj.Value
                                        Dim ObjectiveName As ObjectiveName = GetObjectiveName(NewObjective.ID, MapName)
                                        If ObjectiveName IsNot Nothing Then
                                            NewObjective.Name = ObjectiveName.Name
                                            NewObjective.FullName = ObjectiveName.FullName
                                            If NewObjective.Name.Contains(" ") Then
                                                NewObjective.Name = NewObjective.Name.Split(" ")(0)
                                                NewObjective.FullName = NewObjective.Name.Split(" ")(0)
                                            End If
                                            NewObjective.ObjectiveType = ObjectiveName.ObjectiveType
                                            NewObjective.Points = ObjectiveName.Points
                                        End If
                                    End If

                                    If mObj.Name = "owner" Then NewObjective.Owner = mObj.Value
                                Next
                                mObjectives.Add(NewObjective)
                            Next
                        ElseIf mSubMap.Name = "scores" Then
                            Dim mMapScore As New MapScore
                            With mMapScore
                                .Map = MapName
                                .RedScore = mSubMap.Value.Children.ToList(0).Value(Of Long)()
                                .BlueScore = mSubMap.Value.Children.ToList(1).Value(Of Long)()
                                .GreenScore = mSubMap.Value.Children.ToList(2).Value(Of Long)()
                            End With
                            MapData.MapScores.Add(mMapScore)
                        End If
                    Next
                Next
            ElseIf mChild.Name = "scores" Then
                Dim mMapScore As New MapScore
                With mMapScore
                    .Map = "Total"
                    .RedScore = mChild.Value.Children.ToList(0).Value(Of Long)()
                    .BlueScore = mChild.Value.Children.ToList(1).Value(Of Long)()
                    .GreenScore = mChild.Value.Children.ToList(2).Value(Of Long)()
                End With
                MapData.MapScores.Add(mMapScore)
            End If
        Next

        Return mObjectives
    End Function

    Public Shared Function GetObjectiveName(ByVal ID As String, MapName As String) As ObjectiveName
        For Each mObjectiveName In ObjectiveNames
            If mObjectiveName.ID = ID And mObjectiveName.Map = MapName Then Return mObjectiveName
        Next

        Return Nothing
    End Function

    Public ReadOnly Property CooldownTimer As String
        Get
            Dim result As String = -1
            If OwnerChangeTime <> Nothing Then
                If (Date.Now - OwnerChangeTime).Hours < 2 Then
                    Dim elapsed As Integer = (Date.Now - OwnerChangeTime).TotalSeconds

                    If elapsed <= 300 Then
                        Dim ts As New TimeSpan(0, 0, 300 - elapsed)
                        result = ts.ToString.Substring(4)
                    End If

                End If
            End If

            Return result
        End Get
    End Property

    Public Class ObjectiveName
        Public ID As String
        Public Name As String
        Public FullName As String
        Public ObjectiveType As String
        Public Points As Integer
        Public Map As String

        Public Sub New()
        End Sub

        Public Sub New(ByVal ID As String, ByVal Name As String, ByVal FullName As String, ByVal ObjectiveType As String, ByVal Points As Integer, ByVal Map As String)
            Me.ID = ID
            Me.Name = Name
            Me.FullName = FullName
            Me.ObjectiveType = ObjectiveType
            Me.Points = Points
            Me.Map = Map
        End Sub
    End Class
End Class
