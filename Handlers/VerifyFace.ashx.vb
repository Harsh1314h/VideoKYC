Imports System.IO
Imports System.Web
Imports Newtonsoft.Json
Imports Dapper
Imports VideoKYC.Services

Namespace Handlers
    Public Class VerifyFace
        Implements IHttpHandler

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            Dim responseJson As String = ""
            Try
                Dim sessionId = context.Request.Form("sessionId")

                If context.Request.Files.Count = 0 Then
                    Throw New Exception("No camera frame was uploaded.")
                End If
                Dim file = context.Request.Files(0)

                If String.IsNullOrEmpty(sessionId) Then
                    Throw New Exception("Session ID is missing.")
                End If

                ' Save file to session directory
                Dim uploadsDir = context.Server.MapPath("~/Uploads/" & sessionId)
                If Not Directory.Exists(uploadsDir) Then
                    Directory.CreateDirectory(uploadsDir)
                End If

                Dim liveName = "live_frame.jpg"
                Dim livePath = Path.Combine(uploadsDir, liveName)
                file.SaveAs(livePath)

                ' Locate reference document face photo
                Dim docFaceName = "doc_face.jpg"
                Dim docFacePath = Path.Combine(uploadsDir, docFaceName)

                ' Fallback: if doc_face.jpg doesn't exist, search for any document upload in this folder
                If Not System.IO.File.Exists(docFacePath) Then
                    Dim files = Directory.GetFiles(uploadsDir, "doc_*.jpg")
                    If files.Length > 0 Then
                        docFacePath = files(0)
                        docFaceName = Path.GetFileName(docFacePath)
                    End If
                End If

                Dim score = 0.0
                Dim verified = False

                If System.IO.File.Exists(docFacePath) Then
                    Dim faceSvc As New FaceVerificationService()
                    score = faceSvc.CompareFaces(livePath, docFacePath)
                    
                    ' Verification threshold is 45% for histogram correlation in grayscale
                    verified = (score >= 45.0)
                End If

                ' Log audit trail
                Dim sessionSvc As New SessionService()
                sessionSvc.LogAudit(sessionId, "Face Verification Completed", 
                                    "Compared live camera frame with document photo reference. Server score: " & score & "%. Verified: " & verified & ".", 
                                    "Customer")

                ' Save verification details in DB
                Using conn As System.Data.SqlClient.SqlConnection = Data.DatabaseHelper.GetConnection()
                    Dim sql As String = "INSERT INTO FaceVerifications (SessionId, LiveFramePath, DocPhotoPath, ClientScore, ServerScore, IsVerified, CreatedAt) " &
                              "VALUES (@sid, @lp, @dp, @cs, @ss, @iv, GETUTCDATE())"
                    conn.Execute(sql, New With {
                        .sid = sessionId,
                        .lp = "~/Uploads/" & sessionId & "/" & liveName,
                        .dp = If(System.IO.File.Exists(docFacePath), "~/Uploads/" & sessionId & "/" & docFaceName, Nothing),
                        .cs = If(verified, 90.0, 30.0),
                        .ss = score,
                        .iv = If(verified, 1, 0)
                    })
                End Using

                responseJson = JsonConvert.SerializeObject(New With {
                    .serverScore = score,
                    .verified = verified
                })

            Catch ex As Exception
                responseJson = JsonConvert.SerializeObject(New With {
                    .serverScore = 0.0,
                    .verified = False,
                    .errorMsg = ex.Message
                })
            End Try

            context.Response.ContentType = "application/json"
            context.Response.Write(responseJson)
        End Sub

        Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
            Get
                Return False
            End Get
        End Property
    End Class
End Namespace
