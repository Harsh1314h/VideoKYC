Imports System.Data.SqlClient
Imports Dapper
Imports VideoKYC.Services

Partial Public Class AgentSession
    Inherits System.Web.UI.Page

    Protected lblCustName As Global.System.Web.UI.WebControls.Label
    Protected hdnSessionId As Global.System.Web.UI.WebControls.HiddenField

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Verify agent authentication
        If Not User.Identity.IsAuthenticated OrElse Session("AgentId") Is Nothing Then
            FormsAuthentication.SignOut()
            Response.Redirect("Login.aspx")
            Return
        End If

        ' Read session ID from query parameters
        Dim sessionId = Request.QueryString("sid")
        If String.IsNullOrEmpty(sessionId) Then
            Response.Redirect("Queue.aspx")
            Return
        End If

        ' Load session and bind customer name
        Dim sessionSvc As New SessionService()
        Dim sessionData = sessionSvc.GetSession(sessionId)
        
        If sessionData IsNot Nothing Then
            If sessionData.Status = "Approved" OrElse sessionData.Status = "Rejected" Then
                Response.Redirect("Queue.aspx")
                Return
            End If
            lblCustName.Text = sessionData.CustomerName
            hdnSessionId.Value = sessionId

            ' Check if verifications are already completed in database
            Dim docVerified As Boolean = False
            Dim faceVerified As Boolean = False
            Dim voiceVerified As Boolean = False

            Using conn As SqlConnection = Data.DatabaseHelper.GetConnection()
                docVerified = conn.ExecuteScalar(Of Boolean)("SELECT COALESCE((SELECT TOP 1 IsVerified FROM DocumentVerifications WHERE SessionId = @sid AND IsVerified = 1), 0)", New With {.sid = sessionId})
                faceVerified = conn.ExecuteScalar(Of Boolean)("SELECT COALESCE((SELECT TOP 1 IsVerified FROM FaceVerifications WHERE SessionId = @sid AND IsVerified = 1), 0)", New With {.sid = sessionId})
                voiceVerified = conn.ExecuteScalar(Of Boolean)("SELECT COALESCE((SELECT TOP 1 IsVerified FROM VoiceVerifications WHERE SessionId = @sid AND IsVerified = 1), 0)", New With {.sid = sessionId})
            End Using

            ' Register script to initialize client-side verification flags
            Dim initScript = "var initialDocVerified = " & docVerified.ToString().ToLower() & "; " &
                             "var initialFaceVerified = " & faceVerified.ToString().ToLower() & "; " &
                             "var initialVoiceVerified = " & voiceVerified.ToString().ToLower() & ";"
            ClientScript.RegisterStartupScript(Me.GetType(), "InitVerifications", initScript, True)
        Else
            Response.Redirect("Queue.aspx")
        End If
    End Sub
End Class
