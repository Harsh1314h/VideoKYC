Imports OpenCvSharp

Namespace Services
    Public Class FaceVerificationService
        ''' <summary>
        ''' Compares a live face image and the cropped face photo from a document.
        ''' Returns a similarity score from -100 to +100 (correlation coefficient).
        ''' </summary>
        Public Function CompareFaces(livePath As String, docPath As String) As Double
            Try
                Using liveImg = Cv2.ImRead(livePath, ImreadModes.Grayscale)
                Using docImg = Cv2.ImRead(docPath, ImreadModes.Grayscale)
                    If liveImg.Empty() OrElse docImg.Empty() Then Return 0

                    Using liveResized As New Mat()
                    Using docResized As New Mat()
                        ' Resize to standard resolution for uniform comparison
                        Using iaLive As InputArray = InputArray.Create(liveImg)
                            Using oaLiveResized As OutputArray = OutputArray.Create(liveResized)
                                Cv2.Resize(iaLive, oaLiveResized, New Size(200, 200))
                            End Using
                        End Using

                        Using iaDoc As InputArray = InputArray.Create(docImg)
                            Using oaDocResized As OutputArray = OutputArray.Create(docResized)
                                Cv2.Resize(iaDoc, oaDocResized, New Size(200, 200))
                            End Using
                        End Using

                        Using liveHist As New Mat()
                        Using docHist As New Mat()
                            Dim channels As Integer() = {0}
                            Dim histSize As Integer() = {256}
                            Dim ranges As Rangef() = {New Rangef(0, 256)}

                            ' Calculate histograms
                            Using oaLiveHist As OutputArray = OutputArray.Create(liveHist)
                                Cv2.CalcHist(New Mat() {liveResized}, channels, Nothing, oaLiveHist, 1, histSize, ranges)
                            End Using

                            Using oaDocHist As OutputArray = OutputArray.Create(docHist)
                                Cv2.CalcHist(New Mat() {docResized}, channels, Nothing, oaDocHist, 1, histSize, ranges)
                            End Using

                            ' Normalize histograms
                            Using iaLiveHist As InputArray = InputArray.Create(liveHist)
                                Using oaLiveHist As OutputArray = OutputArray.Create(liveHist)
                                    Cv2.Normalize(iaLiveHist, oaLiveHist, 0, 1, NormTypes.MinMax)
                                End Using
                            End Using

                            Using iaDocHist As InputArray = InputArray.Create(docHist)
                                Using oaDocHist As OutputArray = OutputArray.Create(docHist)
                                    Cv2.Normalize(iaDocHist, oaDocHist, 0, 1, NormTypes.MinMax)
                                End Using
                            End Using

                            ' Compare using correlation method
                            Dim correlation As Double
                            Using iaLiveHist As InputArray = InputArray.Create(liveHist)
                                Using iaDocHist As InputArray = InputArray.Create(docHist)
                                    correlation = Cv2.CompareHist(iaLiveHist, iaDocHist, HistCompMethods.Correl)
                                End Using
                            End Using
                            Dim score = correlation * 100

                            ' Cap between 0 and 100
                            If score < 0 Then score = 0
                            Return Math.Round(score, 2)
                        End Using
                        End Using
                    End Using
                    End Using
                End Using
                End Using
            Catch ex As Exception
                ' Fallback in case of exceptions (e.g. invalid files)
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Detects and crops the face from a document or webcam frame using Haar Cascade.
        ''' Saves the cropped face to the specified output path.
        ''' </summary>
        Public Function CropFaceFromImage(imagePath As String, outputPath As String) As Boolean
            Try
                Dim cascadePath As String = System.Web.HttpContext.Current.Server.MapPath("~/models/haarcascade_frontalface_default.xml")
                If Not System.IO.File.Exists(cascadePath) Then Return False

                Using src = Cv2.ImRead(imagePath, ImreadModes.Color)
                    If src.Empty() Then Return False

                    Using cascade As New CascadeClassifier(cascadePath)
                        ' Try rotations to find the one where the face is upright (0, 90 Clockwise, 180, 90 Counter-Clockwise)
                        For rotationIndex As Integer = 0 To 3
                            Using rotated = New Mat()
                                If rotationIndex = 0 Then
                                    src.CopyTo(rotated)
                                ElseIf rotationIndex = 1 Then
                                    Cv2.Rotate(src, rotated, RotateFlags.Rotate90Clockwise)
                                ElseIf rotationIndex = 2 Then
                                    Cv2.Rotate(src, rotated, RotateFlags.Rotate180)
                                ElseIf rotationIndex = 3 Then
                                    Cv2.Rotate(src, rotated, RotateFlags.Rotate90CounterClockwise)
                                End If

                                Using gray = New Mat()
                                    Cv2.CvtColor(rotated, gray, ColorConversionCodes.BGR2GRAY)
                                    Cv2.EqualizeHist(gray, gray)

                                    ' Strict filter: minNeighbors=3, minSize=50x50 (to ignore tiny false positives)
                                    Dim faces = cascade.DetectMultiScale(
                                        gray,
                                        scaleFactor:=1.1,
                                        minNeighbors:=3,
                                        flags:=HaarDetectionTypes.ScaleImage,
                                        minSize:=New Size(45, 45)
                                    )

                                    ' Filter out faces that are disproportionately large (e.g. > 70% of the image size)
                                    Dim validFaceRect As Nullable(Of Rect) = Nothing
                                    For Each f As Rect In faces
                                        If f.Width < (rotated.Width * 0.7) AndAlso f.Height < (rotated.Height * 0.7) Then
                                            validFaceRect = f
                                            Exit For
                                        End If
                                    Next

                                    If validFaceRect.HasValue Then
                                        Dim faceRect = validFaceRect.Value
                                        
                                        ' Add 15% padding around the face bounding box
                                        Dim paddingX = CInt(faceRect.Width * 0.15)
                                        Dim paddingY = CInt(faceRect.Height * 0.15)
                                        
                                        Dim x = Math.Max(0, faceRect.X - paddingX)
                                        Dim y = Math.Max(0, faceRect.Y - paddingY)
                                        Dim w = Math.Min(rotated.Width - x, faceRect.Width + (paddingX * 2))
                                        Dim h = Math.Min(rotated.Height - y, faceRect.Height + (paddingY * 2))
                                        
                                        Dim cropRect As New Rect(x, y, w, h)
                                        Using croppedFace = New Mat(rotated, cropRect)
                                            Cv2.ImWrite(outputPath, croppedFace)
                                        End Using

                                        ' Auto-correct original card image to be rotated upright!
                                        ' Overwriting imagePath rotates the original card image permanently.
                                        Try
                                            Cv2.ImWrite(imagePath, rotated)
                                        Catch ex As Exception
                                            ' Ignore write lock exceptions
                                        End Try

                                        Return True
                                    End If
                                End Using
                            End Using
                        Next
                    End Using
                End Using
                Return False
            Catch ex As Exception
                Return False
            End Try
        End Function
    End Class
End Namespace
