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
            Dim currentSessionId As String = TryCast(Session("SessionId"), String)
            If Not String.IsNullOrEmpty(currentSessionId) Then
                Try
                    Dim sessionSvc As New SessionService()
                    Dim sessionData = sessionSvc.GetSession(currentSessionId)
                    If sessionData IsNot Nothing Then
                        If sessionData.Status = "Waiting" Then
                            sessionSvc.UpdateSessionStatus(currentSessionId, "Rejected", "Cancelled by customer in waiting room")
                        ElseIf sessionData.Status = "InProgress" Then
                            sessionSvc.UpdateSessionStatus(currentSessionId, "Rejected", "Call ended by customer")
                        End If
                    End If
                Catch ex As Exception
                    ' Ignore database errors on cancel/end
                End Try
            End If
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

            ' Enforce name limit of 50 characters
            If name.Length > 50 Then
                Throw New Exception("Full name must not exceed 50 characters.")
            End If

            ' Validate full name contains only letters and spaces, and at least a first and last name
            Dim nameRegex As New System.Text.RegularExpressions.Regex("^[a-zA-Z]{2,}(?:\s+[a-zA-Z]{2,})+$")
            If Not nameRegex.IsMatch(name) Then
                Throw New Exception("Please enter your full name (both first name and last name, containing only letters with at least 2 characters each).")
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
            sessionSvc.CancelActiveSessionsByPhone(phone)
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
                ' Enforce that already completed (Approved/Rejected) sessions cannot be rejoined
                If sessionData.Status = "Approved" OrElse sessionData.Status = "Rejected" Then
                    Throw New Exception("This verification session has already been completed (" & sessionData.Status & "). You cannot rejoin it.")
                End If

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
