Imports System.IO
Imports System.Web
Imports Newtonsoft.Json
Imports Dapper
Imports VideoKYC.Services
Imports VideoKYC.Models

Namespace Handlers
    Public Class UploadDocument
        Implements IHttpHandler

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            Dim responseJson As String = ""
            Try
                ' Validate inputs
                Dim sessionId = context.Request.Form("sessionId")
                Dim docType = context.Request.Form("docType")
                Dim side = context.Request.Form("side")
                
                If String.IsNullOrEmpty(side) Then side = "front"
                side = side.ToLower()
                
                If context.Request.Files.Count = 0 Then
                    Throw New Exception("No document image was uploaded.")
                End If
                Dim file = context.Request.Files(0)
                
                ' Restrict file size to 5MB max
                If file.ContentLength > 5 * 1024 * 1024 Then
                    Throw New Exception("File size exceeds the limit of 5MB. Please upload a smaller file.")
                End If

                If String.IsNullOrEmpty(sessionId) OrElse String.IsNullOrEmpty(docType) Then
                    Throw New Exception("Session ID or Document Type is missing.")
                End If

                ' Block PDF files
                Dim fileExt = Path.GetExtension(file.FileName).ToLower()
                If fileExt = ".pdf" OrElse Not file.ContentType.StartsWith("image/") Then
                    Throw New Exception("PDF uploads are not supported. Please upload an image file.")
                End If

                ' Establish session directories
                Dim uploadsDir = context.Server.MapPath("~/Uploads/" & sessionId)
                If Not Directory.Exists(uploadsDir) Then
                    Directory.CreateDirectory(uploadsDir)
                End If

                ' Save the image file side-specifically
                If String.IsNullOrEmpty(fileExt) Then fileExt = ".jpg"
                Dim originalName = "doc_" & docType.ToLower() & "_" & side & fileExt
                Dim originalPath = Path.Combine(uploadsDir, originalName)
                file.SaveAs(originalPath)

                Dim processingPath = originalPath
                Dim dbImagePath = "~/Uploads/" & sessionId & "/" & originalName

                ' Initialize variables
                Dim existingRecord As DocumentVerification = Nothing
                Dim paths() As String = New String() {"", ""}
                Dim fields As New Dictionary(Of String, String)()
                Dim existingDocNumber As String = ""
                Dim existingIsVerified As Boolean = False

                ' DB transaction to lock and serialize front/back updates
                Dim sessionSvc As New SessionService()

                Using conn As System.Data.SqlClient.SqlConnection = Data.DatabaseHelper.GetConnection()
                    Using trans = conn.BeginTransaction(System.Data.IsolationLevel.Serializable)
                        Try
                            ' Clean up duplicate rows (keep the oldest)
                            Dim cleanupSql As String = "DELETE FROM DocumentVerifications WHERE SessionId = @sid AND DocumentType = @dt " &
                                                       "AND DocVerificationId NOT IN (SELECT MIN(DocVerificationId) FROM DocumentVerifications WHERE SessionId = @sid AND DocumentType = @dt)"
                            conn.Execute(cleanupSql, New With {.sid = sessionId, .dt = docType}, trans)

                            ' Select target record with update lock to block concurrent requests
                            Dim checkSql As String = "SELECT * FROM DocumentVerifications WITH (UPDLOCK, HOLDLOCK) WHERE SessionId = @sid AND DocumentType = @dt"
                            existingRecord = conn.QueryFirstOrDefault(Of DocumentVerification)(checkSql, New With {
                                .sid = sessionId,
                                .dt = docType
                            }, trans)


                            If existingRecord IsNot Nothing Then
                                If Not String.IsNullOrEmpty(existingRecord.ImagePath) Then
                                    Dim dbPaths = existingRecord.ImagePath.Split(","c)
                                    If dbPaths.Length >= 1 Then paths(0) = dbPaths(0)
                                    If dbPaths.Length >= 2 Then paths(1) = dbPaths(1)
                                End If

                                If Not String.IsNullOrEmpty(existingRecord.ExtractedDataJson) Then
                                    Try
                                        fields = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(existingRecord.ExtractedDataJson)
                                    Catch
                                    End Try
                                End If
                                existingDocNumber = existingRecord.DocumentNumber
                                existingIsVerified = existingRecord.IsVerified
                            End If

                            ' Run processing pipeline
                            Dim customerName = ""
                            Dim sessionData = sessionSvc.GetSession(sessionId)
                            If sessionData IsNot Nothing Then customerName = sessionData.CustomerName

                            Dim docSvc As New DocumentVerificationService()
                            Dim result = docSvc.ProcessDocument(processingPath, docType, side, customerName)
                            
                            ' Merge fields
                            For Each kvp As KeyValuePair(Of String, String) In result.Fields
                                fields(kvp.Key) = kvp.Value
                            Next
                            result.Fields = fields

                            ' Update paths
                            If side = "front" Then
                                paths(0) = dbImagePath
                            Else
                                paths(1) = dbImagePath
                            End If
                            Dim finalImagePath As String = String.Join(",", paths)
                            result.ImagePath = finalImagePath

                            Dim finalIsVerified = result.IsVerified
                            Dim finalDocNumber = result.DocumentNumber

                            If side = "back" Then
                                ' Back side keeps front validation state and number
                                finalIsVerified = existingIsVerified
                                finalDocNumber = If(String.IsNullOrEmpty(result.DocumentNumber), existingDocNumber, result.DocumentNumber)
                                result.IsVerified = finalIsVerified
                                result.DocumentNumber = finalDocNumber
                            End If

                            ' Save the front document photo image as doc_face.jpg
                            If side = "front" AndAlso System.IO.File.Exists(originalPath) Then
                                Dim refFacePath = Path.Combine(uploadsDir, "doc_face.jpg")
                                System.IO.File.Copy(originalPath, refFacePath, True)
                            End If

                            ' Save verification details in DB
                            Dim newOcrText = "--- " & side.ToUpper() & " SIDE ---" & vbCrLf & result.RawOcrText
                            If existingRecord IsNot Nothing Then
                                newOcrText = existingRecord.OcrText & vbCrLf & vbCrLf & newOcrText
                            End If
                            result.RawOcrText = newOcrText

                            If existingRecord IsNot Nothing Then
                                Dim updateSql As String = "UPDATE DocumentVerifications SET DocumentNumber = @dn, IsVerified = @iv, ExtractedDataJson = @json, ImagePath = @ip, OcrText = @ocr WHERE SessionId = @sid AND DocumentType = @dt"
                                conn.Execute(updateSql, New With {
                                    .sid = sessionId,
                                    .dt = docType,
                                    .dn = finalDocNumber,
                                    .iv = If(finalIsVerified, 1, 0),
                                    .json = JsonConvert.SerializeObject(result.Fields),
                                    .ip = finalImagePath,
                                    .ocr = newOcrText
                                }, trans)
                            Else
                                Dim insertSql As String = "INSERT INTO DocumentVerifications (SessionId, DocumentType, DocumentNumber, IsVerified, ExtractedDataJson, ImagePath, OcrText, CreatedAt) " &
                                                          "VALUES (@sid, @dt, @dn, @iv, @json, @ip, @ocr, GETUTCDATE())"
                                conn.Execute(insertSql, New With {
                                    .sid = sessionId,
                                    .dt = docType,
                                    .dn = finalDocNumber,
                                    .iv = If(finalIsVerified, 1, 0),
                                    .json = JsonConvert.SerializeObject(result.Fields),
                                    .ip = finalImagePath,
                                    .ocr = newOcrText
                                }, trans)
                            End If

                            ' Commit transaction
                            trans.Commit()

                            ' Log audit trail
                            sessionSvc.LogAudit(sessionId, "Document Uploaded", 
                                                "Uploaded document " & docType & " (" & side & "). Number: " & finalDocNumber & ". Verified: " & finalIsVerified & ".", 
                                                "Customer")

                            responseJson = JsonConvert.SerializeObject(result)

                        Catch ex As Exception
                            trans.Rollback()
                            Throw ex
                        End Try
                    End Using
                End Using
            Catch ex As Exception
                responseJson = JsonConvert.SerializeObject(New With {
                    .DocumentType = "Unknown",
                    .DocumentNumber = "",
                    .IsVerified = False,
                    .RawOcrText = "",
                    .Fields = New Dictionary(Of String, String)() From {{"Error", ex.Message}},
                    .ImagePath = ""
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
