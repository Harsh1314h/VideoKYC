Imports VideoKYC.Services

Partial Public Class Register
    Inherits System.Web.UI.Page

    Protected txtFullName As Global.System.Web.UI.WebControls.TextBox
    Protected txtPhone As Global.System.Web.UI.WebControls.TextBox
    Protected txtSessionId As Global.System.Web.UI.WebControls.TextBox
    Protected hdnActiveTab As Global.System.Web.UI.WebControls.HiddenField
    Protected pnlError As Global.System.Web.UI.WebControls.Panel
    Protected lblError As Global.System.Web.UI.WebControls.Label

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Handle explicit end-call / logout action
        If Request.QueryString("action") = "logout" Then
            Session("SessionId") = Nothing
            Session("CustomerName") = Nothing
            Session("CustomerId") = Nothing
        End If

        ' If already registered, redirect to waiting room
        If Session("SessionId") IsNot Nothing Then
            Response.Redirect("WaitingRoom.aspx")
        End If
    End Sub

    Protected Sub btnSubmit_Click(sender As Object, e As EventArgs)
        Try
            Dim name = txtFullName.Text.Trim()
            Dim phone = txtPhone.Text.Trim()

            If String.IsNullOrEmpty(name) OrElse String.IsNullOrEmpty(phone) Then
                Throw New Exception("Please enter your name and phone number.")
            End If

            ' Validate phone length is exactly 10 digits and only numbers
            If phone.Length <> 10 Then
                Throw New Exception("Mobile number must be exactly 10 digits.")
            End If
            For Each c As Char In phone
                If Not Char.IsDigit(c) Then
                    Throw New Exception("Mobile number must contain only numbers.")
                End If
            Next

            ' Insert to database and create session
            Dim sessionSvc As New SessionService()
            Dim customer = sessionSvc.RegisterCustomer(name, phone)
            Dim sessionId = sessionSvc.CreateSession(customer.CustomerId)

            ' Store details in Session state
            Session("SessionId") = sessionId
            Session("CustomerName") = customer.FullName
            Session("CustomerId") = customer.CustomerId

            ' Redirect to waiting room
            Response.Redirect("WaitingRoom.aspx")
        Catch ex As Exception
            lblError.Text = ex.Message
            pnlError.Visible = True
            hdnActiveTab.Value = "register"
        End Try
    End Sub

    Protected Sub btnRejoin_Click(sender As Object, e As EventArgs)
        Try
            Dim sessionId = txtSessionId.Text.Trim()

            If String.IsNullOrEmpty(sessionId) Then
                Throw New Exception("Please enter a valid Session ID.")
            End If

            ' Validate GUID/UUID format
            Dim parsedGuid As Guid
            If Not Guid.TryParse(sessionId, parsedGuid) Then
                Throw New Exception("Invalid Session ID format. It should be a 36-character UUID.")
            End If

            Dim sessionSvc As New SessionService()
            Dim sessionData = sessionSvc.GetSession(sessionId)

            If sessionData IsNot Nothing Then
                ' Restore session details
                Session("SessionId") = sessionData.SessionId
                Session("CustomerName") = sessionData.CustomerName
                Session("CustomerId") = sessionData.CustomerId

                ' Redirect according to current session status
                If sessionData.Status = "Waiting" Then
                    Response.Redirect("WaitingRoom.aspx")
                Else
                    Response.Redirect("Session.aspx")
                End If
            Else
                Throw New Exception("Session ID not found. Please verify the ID or register a new call.")
            End If
        Catch ex As Exception
            lblError.Text = ex.Message
            pnlError.Visible = True
            hdnActiveTab.Value = "rejoin"
        End Try
    End Sub
End Class
