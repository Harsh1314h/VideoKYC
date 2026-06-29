Imports System.IO
Imports System.Web
Imports System.Configuration
Imports System.Globalization
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

                Dim clientScore = ParseDoubleForm(context.Request.Form("clientScore"))
                Dim clientVerified = ParseBooleanForm(context.Request.Form("clientVerified"))

                ' Save file to session directory
                Dim uploadsDir = context.Server.MapPath("~/Uploads/" & sessionId)
                If Not Directory.Exists(uploadsDir) Then
                    Directory.CreateDirectory(uploadsDir)
                End If

                Dim liveName = "live_frame.jpg"
                Dim livePath = Path.Combine(uploadsDir, liveName)
                file.SaveAs(livePath)

                Dim faceSvc As New FaceVerificationService()
                Dim liveFacePath = Path.Combine(uploadsDir, "live_face_cropped.jpg")
                Dim liveCroppedSuccessfully = faceSvc.CropFaceFromImage(livePath, liveFacePath, False)
                Dim compareLivePath = If(liveCroppedSuccessfully, liveFacePath, livePath)

                ' Locate reference document face photo
                Dim docFaceName = "doc_face.jpg"
                Dim docFacePath = Path.Combine(uploadsDir, docFaceName)

                If System.IO.File.Exists(docFacePath) AndAlso ShouldRebuildDocumentFaceReference(docFacePath) Then
                    System.IO.File.Delete(docFacePath)
                End If

                ' Rebuild the face reference from the uploaded front document if an older run missed it.
                If Not System.IO.File.Exists(docFacePath) Then
                    TryRebuildDocumentFaceReference(faceSvc, uploadsDir, docFacePath)
                End If

                Dim score = 0.0
                Dim finalScore = clientScore
                Dim serverVerified = False
                Dim verified = False
                Dim errorMsg As String = Nothing

                If System.IO.File.Exists(docFacePath) Then
                    score = faceSvc.CompareFaces(compareLivePath, docFacePath)
                    finalScore = Math.Max(score, clientScore)

                    Dim threshold = GetFaceMatchThreshold()
                    serverVerified = (score >= threshold)
                    verified = serverVerified OrElse (clientVerified AndAlso clientScore >= threshold)
                Else
                    errorMsg = "Document face reference was not detected. Please re-upload a clear front-side document image."
                End If

                If Not verified AndAlso Not liveCroppedSuccessfully AndAlso String.IsNullOrEmpty(errorMsg) Then
                    errorMsg = "Live face was not detected clearly. Please center your face and try again."
                End If

                ' Log audit trail
                Dim sessionSvc As New SessionService()
                sessionSvc.LogAudit(sessionId, "Face Verification Completed", 
                                    "Compared live camera frame with document photo reference. Client score: " & clientScore & "%. Server score: " & score & "%. Verified: " & verified & ".", 
                                    "Customer")

                ' Save verification details in DB
                Using conn As System.Data.SqlClient.SqlConnection = Data.DatabaseHelper.GetConnection()
                    Dim sql As String = "INSERT INTO FaceVerifications (SessionId, LiveFramePath, DocPhotoPath, ClientScore, ServerScore, IsVerified, CreatedAt) " &
                              "VALUES (@sid, @lp, @dp, @cs, @ss, @iv, GETDATE())"
                    conn.Execute(sql, New With {
                        .sid = sessionId,
                        .lp = "~/Uploads/" & sessionId & "/" & liveName,
                        .dp = If(System.IO.File.Exists(docFacePath), "~/Uploads/" & sessionId & "/" & docFaceName, Nothing),
                        .cs = clientScore,
                        .ss = score,
                        .iv = If(verified, 1, 0)
                    })
                End Using

                responseJson = JsonConvert.SerializeObject(New With {
                    .serverScore = score,
                    .clientScore = clientScore,
                    .score = finalScore,
                    .verified = verified,
                    .errorMsg = errorMsg
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

        Private Function ParseDoubleForm(value As String) As Double
            Dim parsed As Double = 0
            If Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                Return Math.Max(0, Math.Min(100, parsed))
            End If
            Return 0
        End Function

        Private Function ParseBooleanForm(value As String) As Boolean
            Dim parsed As Boolean = False
            If Boolean.TryParse(value, parsed) Then Return parsed
            Return value = "1"
        End Function

        Private Function GetFaceMatchThreshold() As Double
            Dim threshold = ParseDoubleForm(ConfigurationManager.AppSettings("FaceMatchThreshold"))
            If threshold <= 0 Then Return 50.0
            If threshold <= 1 Then Return threshold * 100.0
            Return threshold
        End Function

        Private Function TryRebuildDocumentFaceReference(faceSvc As FaceVerificationService, uploadsDir As String, docFacePath As String) As Boolean
            For Each candidate As String In Directory.GetFiles(uploadsDir)
                Dim name = Path.GetFileName(candidate).ToLowerInvariant()
                If name.StartsWith("doc_") AndAlso name.Contains("_front") AndAlso Not name.Contains("_processed") AndAlso IsSupportedImage(candidate) Then
                    If faceSvc.CropFaceFromImage(candidate, docFacePath, True) Then Return True
                End If
            Next

            For Each candidate As String In Directory.GetFiles(uploadsDir)
                Dim name = Path.GetFileName(candidate).ToLowerInvariant()
                If name.StartsWith("doc_") AndAlso name <> "doc_face.jpg" AndAlso Not name.Contains("_processed") AndAlso IsSupportedImage(candidate) Then
                    If faceSvc.CropFaceFromImage(candidate, docFacePath, True) Then Return True
                End If
            Next

            Return False
        End Function

        Private Function ShouldRebuildDocumentFaceReference(docFacePath As String) As Boolean
            Try
                Using img = System.Drawing.Image.FromFile(docFacePath)
                    Dim longestSide = Math.Max(img.Width, img.Height)
                    Dim aspectRatio = CDbl(img.Width) / CDbl(img.Height)
                    Return longestSide > 700 OrElse aspectRatio > 1.45 OrElse aspectRatio < 0.55
                End Using
            Catch
                Return True
            End Try
        End Function

        Private Function IsSupportedImage(path As String) As Boolean
            Dim ext = System.IO.Path.GetExtension(path).ToLowerInvariant()
            Return ext = ".jpg" OrElse ext = ".jpeg" OrElse ext = ".png" OrElse ext = ".bmp"
        End Function
    End Class
End Namespace
