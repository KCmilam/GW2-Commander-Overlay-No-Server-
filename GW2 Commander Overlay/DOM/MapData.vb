

Public Class MapData
    Public Shared SelectedServerID As String = "-"
    Public Match As MatchData
    Public Server As ServerData
    Public Objectives As List(Of Objective)
    Public ValidObjectives As String
    Public Shared MapScores As New List(Of MapScore)

    Public Sub New()
        Try
            Match = MatchData.GetMatchData
            Objectives = Objective.GetObjectiveData(Match.ID)
        Catch

        End Try
    End Sub

    Public Sub UpdateObjectives()
        Try
            Dim NewObjectives As List(Of Objective) = Objective.GetObjectiveData(Match.ID)
            For Each CurrentObject In Objectives
                For Each NewObjective In NewObjectives
                    If CurrentObject.FullName = NewObjective.FullName Then
                        If CurrentObject.Owner <> NewObjective.Owner Then
                            CurrentObject.Owner = NewObjective.Owner
                            CurrentObject.OwnerChangeTime = Date.Now
                        End If
                    End If
                Next
            Next

            Dim mValid As String = ""
            For Each mObj As Objective In Objectives
                mValid &= mObj.FullName & "~" & mObj.ObjectiveType.ToLower.Chars(0) & "~" & mObj.Owner.ToLower.Chars(0) & "~" & mObj.CooldownTimer & "|"
            Next
            ValidObjectives = mValid
        Catch x As Exception
            My.Computer.FileSystem.WriteAllText(AppDomain.CurrentDomain.BaseDirectory & "log.txt", Date.Now.ToString & ": " & x.Message, True)
        End Try
    End Sub
End Class
