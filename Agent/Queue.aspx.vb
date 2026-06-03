Imports VideoKYC.Services

Partial Public Class Queue
    Inherits System.Web.UI.Page

    Protected lblAgentName As Global.System.Web.UI.WebControls.Label
    Protected gvSessions As Global.System.Web.UI.WebControls.GridView

    Private ReadOnly _sessionSvc As New SessionService()

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Verify agent is authenticated
        If Not User.Identity.IsAuthenticated OrElse Session("AgentId") Is Nothing Then
            FormsAuthentication.SignOut()
            Response.Redirect("Login.aspx")
            Return
        End If

        ' Set officer welcome details
        If Session("AgentName") IsNot Nothing Then
            lblAgentName.Text = Session("AgentName").ToString()
        End If

        If Not IsPostBack Then
            BindQueue()
        End If
    End Sub

    Private Sub BindQueue()
        Try
            gvSessions.DataSource = _sessionSvc.GetWaitingSessions()
            gvSessions.DataBind()
        Catch
            ' Handle errors if database is locked
        End Try
    End Sub

    Protected Sub btnRefresh_Click(sender As Object, e As EventArgs)
        BindQueue()
    End Sub

    Protected Sub gvSessions_RowCommand(sender As Object, e As GridViewCommandEventArgs)
        If e.CommandName = "JoinCall" Then
            Dim sessionId = e.CommandArgument.ToString()
            Dim agentId = Convert.ToInt32(Session("AgentId"))

            ' Assign the current agent to the customer's call session (marks status 'InProgress')
            _sessionSvc.AssignAgent(sessionId, agentId)

            ' Redirect agent to call session page
            Response.Redirect("Session.aspx?sid=" & sessionId)
        End If
    End Sub

    Protected Sub btnLogOut_Click(sender As Object, e As EventArgs)
        FormsAuthentication.SignOut()
        Session.Clear()
        Response.Redirect("Login.aspx")
    End Sub
End Class
