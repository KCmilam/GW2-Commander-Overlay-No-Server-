Imports System.Net
Imports System.IO

Public Class JSONUtility
    Public Shared Function GetJsonText(ByVal URL As String) As String
        Dim request As HttpWebRequest = WebRequest.Create(URL)
        Dim response As HttpWebResponse = request.GetResponse
        Dim reader As New StreamReader(response.GetResponseStream)

        Dim jsonstring As String = reader.ReadToEnd

        Return jsonstring
    End Function

    Public Shared ReadOnly Property MapScoresString() As String
        Get
            Dim result As String = ""
            For Each mMapScore In MapData.MapScores
                result &= mMapScore.Map.Chars(0) & "~" & mMapScore.RedScore & "~" & mMapScore.BlueScore & "~" & mMapScore.GreenScore & "|"
            Next
            Return result.TrimEnd("|")
        End Get
    End Property
End Class
