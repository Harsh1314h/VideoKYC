Namespace Models
    Public Class KycSession
        Public Property SessionId As String
        Public Property CustomerId As Integer
        Public Property AgentId As Integer?
        Public Property Status As String ' Waiting, InProgress, Approved, Rejected
        Public Property SessionToken As String
        Public Property CreatedAt As DateTime
        Public Property UpdatedAt As DateTime?
        Public Property RejectionReason As String

        ' Joined properties from Customer/Agent tables
        Public Property CustomerName As String
        Public Property CustomerPhone As String
        Public Property AgentName As String
    End Class
End Namespace
