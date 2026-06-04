Imports System.Collections.Generic

Namespace Models
    Public Class DocumentResult
        Public Property DocumentType As String ' Aadhaar, PAN, Passport, DL
        Public Property DocumentNumber As String
        Public Property IsVerified As Boolean
        Public Property RawOcrText As String
        Public Property Fields As New Dictionary(Of String, String)()
        Public Property ImagePath As String
    End Class
End Namespace
