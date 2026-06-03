Imports System.Security.Cryptography
Imports System.Text
Imports Dapper
Imports VideoKYC.Data

Partial Public Class Login
    Inherits System.Web.UI.Page

    Protected txtUsername As Global.System.Web.UI.WebControls.TextBox
    Protected txtPassword As Global.System.Web.UI.WebControls.TextBox
    Protected pnlError As Global.System.Web.UI.WebControls.Panel
    Protected lblError As Global.System.Web.UI.WebControls.Label

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If User.Identity.IsAuthenticated Then
            Response.Redirect("Queue.aspx")
        End If
    End Sub

    Protected Sub btnLogin_Click(sender As Object, e As EventArgs)
        Try
            Dim username = txtUsername.Text.Trim()
            Dim password = txtPassword.Text.Trim()

            If String.IsNullOrEmpty(username) OrElse String.IsNullOrEmpty(password) Then
                Throw New Exception("Please fill in all fields.")
            End If

            ' Hash password using SHA256
            Dim passHash = GetSha256Hash(password)

            ' Authenticate against SQL Server
            Dim agent As Models.Agent = Nothing
            Using conn As System.Data.SqlClient.SqlConnection = DatabaseHelper.GetConnection()
                Dim sql As String = "SELECT * FROM Agents WHERE Username = @u AND PasswordHash = @h AND IsActive = 1"
                agent = conn.QueryFirstOrDefault(Of Models.Agent)(sql, New With {.u = username, .h = passHash})
            End Using

            If agent IsNot Nothing Then
                ' Save credentials to session variables
                Session("AgentId") = agent.AgentId
                Session("AgentName") = agent.FullName

                ' Establish forms auth cookie and redirect
                FormsAuthentication.RedirectFromLoginPage(username, False)
            Else
                Throw New Exception("Invalid username or password.")
            End If
        Catch ex As Exception
            lblError.Text = ex.Message
            pnlError.Visible = True
        End Try
    End Sub

    Private Function GetSha256Hash(input As String) As String
        Using sha As SHA256 = SHA256.Create()
            Dim bytes As Byte() = sha.ComputeHash(Encoding.UTF8.GetBytes(input))
            Dim sb As New StringBuilder()
            For i As Integer = 0 To bytes.Length - 1
                sb.Append(bytes(i).ToString("x2"))
            Next
            Return sb.ToString()
        End Using
    End Function
End Class
