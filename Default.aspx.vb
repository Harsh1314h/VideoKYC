Public Class _Default
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Check if user is authenticated as agent, and redirect to Agent/Queue.aspx
        If User.Identity.IsAuthenticated Then
            Response.Redirect("~/Agent/Queue.aspx")
        End If
    End Sub
End Class
