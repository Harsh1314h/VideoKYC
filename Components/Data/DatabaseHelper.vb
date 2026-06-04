Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient

Namespace Data
    Public Class DatabaseHelper
        Private Shared ReadOnly ConnectionString As String = 
            ConfigurationManager.ConnectionStrings("KycDb").ConnectionString

        Public Shared Function GetConnection() As IDbConnection
            Dim conn As New SqlConnection(ConnectionString)
            conn.Open()
            Return conn
        End Function
    End Class
End Namespace
