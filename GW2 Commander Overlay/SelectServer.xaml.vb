Public Class SelectServer

    Public Sub New()
        InitializeComponent()
        For Each mServerData As ServerData In ServerData.Instance
            cbServer.Items.Add(mServerData.Name)
        Next
    End Sub

    Private Sub btnOK_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnOK.Click
        For Each mServer As ServerData In ServerData.Instance
            If mServer.Name = cbServer.Text Then
                MapData.SelectedServerID = mServer.ID
                My.Settings.Server = mServer.ID
                My.Settings.Save()
                Exit For
            End If
        Next

        Me.Close()
    End Sub
End Class
