Imports VideoKYC.Services

Partial Public Class Register
    Inherits System.Web.UI.Page

    Protected txtFullName As Global.System.Web.UI.WebControls.TextBox
    Protected txtPhone As Global.System.Web.UI.WebControls.TextBox
    Protected pnlError As Global.System.Web.UI.WebControls.Panel
    Protected lblError As Global.System.Web.UI.WebControls.Label

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
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
                Throw New Exception("Please fill in all fields.")
            End If

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
        End Try
    End Sub
End Class
