
Public Class MapScore
    Public Map As String
    Public RedScore As Integer
    Public BlueScore As Integer
    Public GreenScore As Integer

    Public RedPotential As Integer = 0
    Public BluePotential As Integer = 0
    Public GreenPotential As Integer = 0

    Public ReadOnly Property SortedScores As List(Of String)
        Get
            Dim mSorted As New List(Of String)

            Dim temp As New List(Of Integer)
            temp.Add(RedScore)
            temp.Add(BlueScore)
            temp.Add(GreenScore)

            temp.Sort()

            For Each mScore As Integer In temp
                If mScore = RedScore Then
                    mSorted.Add("Red~" & mScore)
                ElseIf mScore = BlueScore Then
                    mSorted.Add("Blue~" & mScore)
                Else
                    mSorted.Add("Green~" & mScore)
                End If
            Next

            Return mSorted
        End Get
    End Property
End Class
