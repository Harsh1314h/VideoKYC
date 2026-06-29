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

                    Using gray = New Mat()
                        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY)
                        Cv2.EqualizeHist(gray, gray)

                        Using cascade As New CascadeClassifier(cascadePath)
                            Dim faces = cascade.DetectMultiScale(
                                gray,
                                scaleFactor:=1.1,
                                minNeighbors:=2, ' Low neighbors since document photo faces can be small/coarse
                                flags:=HaarDetectionTypes.ScaleImage,
                                minSize:=New Size(30, 30)
                            )

                            If faces.Length > 0 Then
                                ' Crop the first detected face
                                Dim faceRect = faces(0)
                                
                                ' Add 15% padding around the face bounding box
                                Dim paddingX = CInt(faceRect.Width * 0.15)
                                Dim paddingY = CInt(faceRect.Height * 0.15)
                                
                                Dim x = Math.Max(0, faceRect.X - paddingX)
                                Dim y = Math.Max(0, faceRect.Y - paddingY)
                                Dim w = Math.Min(src.Width - x, faceRect.Width + (paddingX * 2))
                                Dim h = Math.Min(src.Height - y, faceRect.Height + (paddingY * 2))
                                
                                Dim cropRect As New Rect(x, y, w, h)
                                Using croppedFace = New Mat(src, cropRect)
                                    Cv2.ImWrite(outputPath, croppedFace)
                                End Using
                                Return True
                            End If
                        End Using
                    End Using
                End Using
                Return False
            Catch ex As Exception
                ' Fallback to copying entire file or returning false
                Return False
            End Try
        End Function
    End Class
End Namespace
