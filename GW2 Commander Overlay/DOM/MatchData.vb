
Imports Newtonsoft.Json.Linq

Public Class MatchData
    Public ID As String
    Public RedServer As String
    Public BlueServer As String
    Public GreenServer As String

    Public RedServerName As String
    Public BlueServerName As String
    Public GreenServerName As String


    Public Shared Function GetMatchData() As MatchData
        Dim mMatch As New MatchData
        Dim json As JObject = JObject.Parse(JSONUtility.GetJsonText("https://api.guildwars2.com/v1/wvw/matches.json"))

        For Each mChild As JProperty In json.Children.ToList
            For Each mItem As JToken In mChild.Value
                Dim MatchID As String = ""
                For Each mLine As JProperty In mItem.Children.ToList
                    If mLine.Name = "red_world_id" Or mLine.Name = "blue_world_id" Or mLine.Name = "green_world_id" Then
                        'If mLine.Value = "1016" Then
                        If My.Settings.Server = "-" Then
                            Dim mSelectServer As New SelectServer
                            mSelectServer.ShowDialog()
                        End If
                        If mLine.Value = My.Settings.Server Then
                            For Each mValue As JProperty In mItem.Children.ToList
                                If mValue.Name = "wvw_match_id" Then
                                    mMatch.ID = mValue.Value
                                ElseIf mValue.Name = "red_world_id" Then
                                    mMatch.RedServer = mValue.Value
                                    mMatch.RedServerName = ServerData.GetServerName(mMatch.RedServer)
                                ElseIf mValue.Name = "blue_world_id" Then
                                    mMatch.BlueServer = mValue.Value
                                    mMatch.BlueServerName = ServerData.GetServerName(mMatch.BlueServer)
                                ElseIf mValue.Name = "green_world_id" Then
                                    mMatch.GreenServer = mValue.Value
                                    mMatch.GreenServerName = ServerData.GetServerName(mMatch.GreenServer)
                                End If
                            Next
                        End If
                    End If
                Next
            Next
        Next

        Return mMatch
    End Function

    Public Shared Function GetMapScores(ByVal MatchID As String) As List(Of MapScore)
        Dim mMapScores As New List(Of MapScore)
        MapData.MapScores.Clear()
        Dim json As JObject = JObject.Parse(JSONUtility.GetJsonText("https://api.guildwars2.com/v1/wvw/match_details.json?match_id=" & MatchID))

        Dim RedTotalPotential As Integer = 0
        Dim BlueTotalPotential As Integer = 0
        Dim GreenTotalPotential As Integer = 0

        For Each mChild As JProperty In json.Children.ToList
            If mChild.Name = "maps" Then
                For Each mMap As JToken In mChild.Value.Children.ToList
                    Dim MapName As String = ""

                    For Each mSubMap As JProperty In mMap.Children.ToList
                        If mSubMap.Name = "type" Then
                            MapName = mSubMap.Value
                        ElseIf mSubMap.Name = "scores" Then
                            Dim mMapScore As New MapScore
                            With mMapScore
                                .Map = MapName
                                .RedScore = mSubMap.Value.Children.ToList(0).Value(Of Long)()
                                .BlueScore = mSubMap.Value.Children.ToList(1).Value(Of Long)()
                                .GreenScore = mSubMap.Value.Children.ToList(2).Value(Of Long)()

                                For Each mObjective As Objective In Objective.GetObjectiveData(MatchID)
                                    If mObjective.Map = MapName Then
                                        Select Case mObjective.Owner
                                            Case "Red" : .RedPotential += mObjective.Points
                                            Case "Blue" : .BluePotential += mObjective.Points
                                            Case "Green" : .GreenPotential += mObjective.Points
                                        End Select
                                    End If
                                Next
                            End With
                            mMapScores.Add(mMapScore)
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
                mMapScores.Add(mMapScore)
            End If
        Next

        Dim TotalMapScore As MapScore = Nothing
        For Each mScore As MapScore In mMapScores
            If mScore.Map = "Total" Then
                TotalMapScore = mScore
            End If
        Next

        For Each mScore As MapScore In mMapScores
            If mScore.Map <> "Total" Then
                TotalMapScore.RedPotential += mScore.RedPotential
                TotalMapScore.BluePotential += mScore.BluePotential
                TotalMapScore.GreenPotential += mScore.GreenPotential
            End If
        Next

        Return mMapScores
    End Function
End Class
