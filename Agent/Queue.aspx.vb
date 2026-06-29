Imports VideoKYC.Services

Partial Public Class Queue
    Inherits System.Web.UI.Page

    Protected lblAgentName As Global.System.Web.UI.WebControls.Label
    Protected gvSessions As Global.System.Web.UI.WebControls.GridView
    Protected pnlActiveSessions As Global.System.Web.UI.WebControls.Panel
    Protected gvActiveSessions As Global.System.Web.UI.WebControls.GridView
    Protected gvApproved As Global.System.Web.UI.WebControls.GridView

    Private ReadOnly _sessionSvc As New SessionService()

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Verify agent is authenticated
        If Not User.Identity.IsAuthenticated OrElse Session("AgentId") Is Nothing Then
            FormsAuthentication.SignOut()
            Response.Redirect("Login.aspx")
            Return
        End If

        ' Handle agent leaving session prematurely
        If Request.QueryString("action") = "leave" Then
            Dim leaveSessionId = Request.QueryString("sid")
            If Not String.IsNullOrEmpty(leaveSessionId) Then
                ' Keep the session as InProgress so the agent can reconnect to it from the dashboard reconnect panel
                Try
                    Dim sessionData = _sessionSvc.GetSession(leaveSessionId)
                    ' We preserve the InProgress state so the agent sees the Reconnect option
                Catch ex As Exception
                    ' Ignore database errors on leave
                End Try
            End If
            Response.Redirect("Queue.aspx")
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
            Dim agentId = Convert.ToInt32(Session("AgentId"))
            Dim activeSessions = _sessionSvc.GetActiveSessionsForAgent(agentId).ToList()
            If activeSessions.Count > 0 Then
                pnlActiveSessions.Visible = True
                gvActiveSessions.DataSource = activeSessions
                gvActiveSessions.DataBind()
            Else
                pnlActiveSessions.Visible = False
            End If

            gvSessions.DataSource = _sessionSvc.GetWaitingSessions()
            gvSessions.DataBind()

            gvApproved.DataSource = _sessionSvc.GetApprovedSessions()
            gvApproved.DataBind()
        Catch
            ' Handle errors if database is locked
        End Try
    End Sub

    Protected Sub btnRefresh_Click(sender As Object, e As EventArgs)
        BindQueue()
    End Sub

    Protected Sub gvActiveSessions_RowCommand(sender As Object, e As GridViewCommandEventArgs)
        If e.CommandName = "ReconnectCall" Then
            Dim sessionId = e.CommandArgument.ToString()
            Response.Redirect("Session.aspx?sid=" & sessionId)
        End If
    End Sub

    Protected Sub gvSessions_RowCommand(sender As Object, e As GridViewCommandEventArgs)
        If e.CommandName = "JoinCall" Then
            Dim sessionId = e.CommandArgument.ToString()
            
            ' Validate that the session is still in 'Waiting' status before assigning the agent
            Dim sessionData = _sessionSvc.GetSession(sessionId)
            If sessionData Is Nothing Then
                BindQueue()
                Return
            End If
            
            If sessionData.Status <> "Waiting" Then
                BindQueue()
                ClientScript.RegisterStartupScript(Me.GetType(), "SessionClosedAlert", "alert('This session is no longer available (it may have been cancelled or picked up by another officer).');", True)
                Return
            End If

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
