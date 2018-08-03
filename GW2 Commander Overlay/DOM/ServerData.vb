
Imports Newtonsoft.Json.Linq

Public Class ServerData
    Public ID As String
    Public Name As String

    Private Shared mInstance As List(Of ServerData)

    Public Shared ReadOnly Property Instance As List(Of ServerData)
        Get
            If mInstance Is Nothing Then
                mInstance = GetServerData()
            End If

            Return mInstance
        End Get
    End Property

    Public Shared Function GetServerData() As List(Of ServerData)
        Dim mServers As New List(Of ServerData)

        'Dim jsonText As String = GetJsonText("https://api.guildwars2.com/v1/world_names.json")
        Dim jsonText As String = JSONUtility.GetJsonText("https://raw.githubusercontent.com/codemasher/gw2api-tools/master/json/gw2_worlds.json")
        jsonText = "{""servers"": " & jsonText & "}"
        Dim json As JObject = JObject.Parse(jsonText)
        For Each mChild As JProperty In json.Children.ToList()
            For Each mServer As JToken In mChild.Value
                Dim NewServer As New ServerData
                For Each mItem As JProperty In mServer.Children.ToList
                    If mItem.Name = "world_id" Then
                        NewServer.ID = mItem.Value
                    ElseIf mItem.Name = "name_en" Then
                        NewServer.Name = mItem.Value
                    End If
                Next
                mServers.Add(NewServer)
            Next
        Next

        Return mServers
    End Function

    Public Shared Function GetServerName(ByVal ServerID As String) As String
        For Each mServer As ServerData In Instance
            If mServer.ID = ServerID Then Return mServer.Name
        Next
        Return ""
    End Function
End Class
