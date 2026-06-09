Imports System

Namespace Models
    Public Class DocumentVerification
        Public Property DocVerificationId As Integer
        Public Property SessionId As String
        Public Property DocumentType As String
        Public Property DocumentNumber As String
        Public Property IsVerified As Boolean
        Public Property ExtractedDataJson As String
        Public Property ImagePath As String
        Public Property OcrText As String
        Public Property CreatedAt As DateTime
    End Class
End Namespace
