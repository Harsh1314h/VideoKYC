Imports OpenCvSharp
Imports System.IO

Namespace Services
    Public Class FaceVerificationService
        ''' <summary>
        ''' Compares a live face image and the cropped face photo from a document.
        ''' Returns a lighting-normalized similarity score from 0 to 100.
        ''' </summary>
        Public Function CompareFaces(livePath As String, docPath As String) As Double
            Try
                Using liveImg = Cv2.ImRead(livePath, ImreadModes.Color)
                Using docImg = Cv2.ImRead(docPath, ImreadModes.Color)
                    If liveImg.Empty() OrElse docImg.Empty() Then Return 0

                    Using livePrepared As Mat = PrepareFaceForComparison(liveImg)
                    Using docPrepared As Mat = PrepareFaceForComparison(docImg)
                        Dim histogramScore = CalculateHistogramScore(livePrepared, docPrepared)
                        Dim structureScore = CalculateTemplateScore(livePrepared, docPrepared)
                        Dim edgeScore = CalculateEdgeScore(livePrepared, docPrepared)

                        ' Blend intensity, structure, and edge similarity so exposure changes do not dominate.
                        Dim score = (histogramScore * 0.35) + (structureScore * 0.45) + (edgeScore * 0.2)
                        Return Math.Round(ClampScore(score), 2)
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
        Public Function CropFaceFromImage(imagePath As String, outputPath As String, Optional isDocumentImage As Boolean = False) As Boolean
            Try
                Dim cascadePath As String = ResolveCascadePath()
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

                                Dim validFaceRect As Nullable(Of Rect) = DetectBestFace(rotated, cascade, isDocumentImage)

                                If validFaceRect.HasValue Then
                                    SaveFaceCrop(rotated, validFaceRect.Value, outputPath)

                                    ' Auto-correct uploaded document images to the detected upright orientation.
                                    If isDocumentImage Then
                                        Try
                                            Cv2.ImWrite(imagePath, rotated)
                                        Catch ex As Exception
                                            ' Ignore write lock exceptions
                                        End Try
                                    End If

                                    Return True
                                End If
                            End Using
                        Next

                        If isDocumentImage Then
                            Return TryCropDocumentPhotoFallback(src, outputPath)
                        End If
                    End Using
                End Using
                Return False
            Catch ex As Exception
                Return False
            End Try
        End Function

        Private Function PrepareFaceForComparison(src As Mat) As Mat
            Dim gray As New Mat()
            If src.Channels() = 1 Then
                src.CopyTo(gray)
            Else
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY)
            End If

            Dim resized As New Mat()
            Cv2.Resize(gray, resized, New Size(160, 160))
            gray.Dispose()

            Dim normalized As New Mat()
            Using clahe = Cv2.CreateCLAHE(2.0, New Size(8, 8))
                clahe.Apply(resized, normalized)
            End Using
            resized.Dispose()

            Cv2.GaussianBlur(normalized, normalized, New Size(3, 3), 0)
            Return normalized
        End Function

        Private Function CalculateHistogramScore(liveFace As Mat, docFace As Mat) As Double
            Using liveHist As New Mat()
            Using docHist As New Mat()
                Dim channels As Integer() = {0}
                Dim histSize As Integer() = {128}
                Dim ranges As Rangef() = {New Rangef(0, 256)}

                Cv2.CalcHist(New Mat() {liveFace}, channels, Nothing, liveHist, 1, histSize, ranges)
                Cv2.CalcHist(New Mat() {docFace}, channels, Nothing, docHist, 1, histSize, ranges)
                Cv2.Normalize(liveHist, liveHist, 1.0, 0.0, NormTypes.L1)
                Cv2.Normalize(docHist, docHist, 1.0, 0.0, NormTypes.L1)

                Dim correlation = Cv2.CompareHist(liveHist, docHist, HistCompMethods.Correl)
                Dim bhattacharyya = Cv2.CompareHist(liveHist, docHist, HistCompMethods.Bhattacharyya)
                Dim correlationScore = ClampScore((correlation + 1.0) * 50.0)
                Dim distanceScore = ClampScore((1.0 - Math.Min(1.0, bhattacharyya)) * 100.0)

                Return (correlationScore * 0.6) + (distanceScore * 0.4)
            End Using
            End Using
        End Function

        Private Function CalculateTemplateScore(liveFace As Mat, docFace As Mat) As Double
            Using result As New Mat()
                Cv2.MatchTemplate(liveFace, docFace, result, TemplateMatchModes.CCoeffNormed)

                Dim minVal As Double = 0
                Dim maxVal As Double = 0
                Dim minLoc As New Point()
                Dim maxLoc As New Point()
                Cv2.MinMaxLoc(result, minVal, maxVal, minLoc, maxLoc)

                Return ClampScore((maxVal + 1.0) * 50.0)
            End Using
        End Function

        Private Function CalculateEdgeScore(liveFace As Mat, docFace As Mat) As Double
            Using liveEdges As New Mat()
            Using docEdges As New Mat()
            Using result As New Mat()
                Cv2.Canny(liveFace, liveEdges, 80, 160)
                Cv2.Canny(docFace, docEdges, 80, 160)
                Cv2.MatchTemplate(liveEdges, docEdges, result, TemplateMatchModes.CCoeffNormed)

                Dim minVal As Double = 0
                Dim maxVal As Double = 0
                Dim minLoc As New Point()
                Dim maxLoc As New Point()
                Cv2.MinMaxLoc(result, minVal, maxVal, minLoc, maxLoc)

                Return ClampScore((maxVal + 1.0) * 50.0)
            End Using
            End Using
            End Using
        End Function

        Private Function DetectBestFace(image As Mat, cascade As CascadeClassifier, isDocumentImage As Boolean) As Nullable(Of Rect)
            Using gray As New Mat()
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY)
                Cv2.EqualizeHist(gray, gray)

                Dim neighborOptions As Integer() = If(isDocumentImage, New Integer() {5, 3, 2}, New Integer() {5, 4, 3})
                Dim scaleOptions As Double() = New Double() {1.05, 1.1}
                Dim minSize = If(isDocumentImage, New Size(30, 30), New Size(55, 55))

                Dim bestRect As Nullable(Of Rect) = Nothing
                Dim bestScore As Double = -1

                For Each scaleFactor As Double In scaleOptions
                    For Each minNeighbors As Integer In neighborOptions
                        Dim faces = cascade.DetectMultiScale(
                            gray,
                            scaleFactor:=scaleFactor,
                            minNeighbors:=minNeighbors,
                            flags:=HaarDetectionTypes.ScaleImage,
                            minSize:=minSize
                        )

                        For Each face As Rect In faces
                            Dim score = ScoreFaceCandidate(face, image.Width, image.Height, isDocumentImage)
                            If score > bestScore Then
                                bestScore = score
                                bestRect = face
                            End If
                        Next

                        If bestRect.HasValue Then Return bestRect
                    Next
                Next

                Return Nothing
            End Using
        End Function

        Private Function ScoreFaceCandidate(face As Rect, imageWidth As Integer, imageHeight As Integer, isDocumentImage As Boolean) As Double
            If face.Width <= 0 OrElse face.Height <= 0 OrElse imageWidth <= 0 OrElse imageHeight <= 0 Then Return -1

            Dim aspect = CDbl(face.Width) / CDbl(face.Height)
            If aspect < 0.75 OrElse aspect > 1.35 Then Return -1

            Dim widthRatio = CDbl(face.Width) / CDbl(imageWidth)
            Dim heightRatio = CDbl(face.Height) / CDbl(imageHeight)

            If isDocumentImage Then
                If widthRatio < 0.035 OrElse heightRatio < 0.035 Then Return -1
                If widthRatio > 0.45 OrElse heightRatio > 0.45 Then Return -1
            Else
                If widthRatio < 0.08 OrElse heightRatio < 0.08 Then Return -1
                If widthRatio > 0.8 OrElse heightRatio > 0.8 Then Return -1
            End If

            Dim areaScore = CDbl(face.Width * face.Height)
            Dim centerX = face.X + (face.Width / 2.0)
            Dim centerY = face.Y + (face.Height / 2.0)

            If isDocumentImage Then
                Dim centerXRatio = centerX / imageWidth
                Dim centerYRatio = centerY / imageHeight
                Dim isPortraitDocument = imageHeight > (imageWidth * 1.15)

                If isPortraitDocument Then
                    If centerXRatio > 0.45 OrElse centerYRatio < 0.7 Then Return -1
                    Dim distance = Math.Abs(centerXRatio - 0.25) + Math.Abs(centerYRatio - 0.78)
                    Dim sizePenalty = Math.Abs(widthRatio - 0.09) + Math.Abs(heightRatio - 0.06)
                    Return Math.Max(1, 100000 - (distance * 120000) - (sizePenalty * 80000))
                Else
                    If centerXRatio > 0.55 OrElse centerYRatio < 0.18 OrElse centerYRatio > 0.78 Then Return -1
                    Dim distance = Math.Abs(centerXRatio - 0.2) + Math.Abs(centerYRatio - 0.42)
                    Dim sizePenalty = Math.Abs(widthRatio - 0.14) + Math.Abs(heightRatio - 0.18)
                    Return Math.Max(1, 100000 - (distance * 100000) - (sizePenalty * 60000))
                End If
            End If

            Dim dx = Math.Abs(centerX - (imageWidth / 2.0)) / (imageWidth / 2.0)
            Dim dy = Math.Abs(centerY - (imageHeight / 2.0)) / (imageHeight / 2.0)
            Dim centerWeight = Math.Max(0.45, 1.0 - ((dx + dy) * 0.25))
            Return areaScore * centerWeight
        End Function

        Private Sub SaveFaceCrop(image As Mat, faceRect As Rect, outputPath As String)
            Dim paddingX = CInt(faceRect.Width * 0.25)
            Dim paddingTop = CInt(faceRect.Height * 0.25)
            Dim paddingBottom = CInt(faceRect.Height * 0.35)

            Dim x = Math.Max(0, faceRect.X - paddingX)
            Dim y = Math.Max(0, faceRect.Y - paddingTop)
            Dim w = Math.Min(image.Width - x, faceRect.Width + (paddingX * 2))
            Dim h = Math.Min(image.Height - y, faceRect.Height + paddingTop + paddingBottom)

            Dim cropRect As New Rect(x, y, w, h)
            Using croppedFace = New Mat(image, cropRect)
                Cv2.ImWrite(outputPath, croppedFace)
            End Using
        End Sub

        Private Function TryCropDocumentPhotoFallback(image As Mat, outputPath As String) As Boolean
            Dim fallbackRects As Rect()
            If image.Height > (image.Width * 1.15) Then
                fallbackRects = New Rect() {
                    MakeSafeRect(image, 0.04, 0.7, 0.34, 0.22),
                    MakeSafeRect(image, 0.02, 0.62, 0.4, 0.32),
                    MakeSafeRect(image, 0.08, 0.65, 0.35, 0.28)
                }
            Else
                fallbackRects = New Rect() {
                    MakeSafeRect(image, 0.03, 0.18, 0.32, 0.48),
                    MakeSafeRect(image, 0.03, 0.28, 0.35, 0.52),
                    MakeSafeRect(image, 0.1, 0.62, 0.34, 0.32)
                }
            End If

            For Each cropArea As Rect In fallbackRects
                If cropArea.Width > 20 AndAlso cropArea.Height > 20 Then
                    Using croppedFace = New Mat(image, cropArea)
                        Cv2.ImWrite(outputPath, croppedFace)
                    End Using
                    Return True
                End If
            Next

            Return False
        End Function

        Private Function MakeSafeRect(image As Mat, xRatio As Double, yRatio As Double, widthRatio As Double, heightRatio As Double) As Rect
            Dim x = Math.Max(0, CInt(image.Width * xRatio))
            Dim y = Math.Max(0, CInt(image.Height * yRatio))
            Dim w = Math.Min(image.Width - x, CInt(image.Width * widthRatio))
            Dim h = Math.Min(image.Height - y, CInt(image.Height * heightRatio))
            Return New Rect(x, y, w, h)
        End Function

        Private Function ClampScore(value As Double) As Double
            If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return 0
            If value < 0 Then Return 0
            If value > 100 Then Return 100
            Return value
        End Function

        Private Function ResolveCascadePath() As String
            Try
                If System.Web.HttpContext.Current IsNot Nothing Then
                    Dim mappedPath = System.Web.HttpContext.Current.Server.MapPath("~/models/haarcascade_frontalface_default.xml")
                    If File.Exists(mappedPath) Then Return mappedPath
                End If
            Catch
            End Try

            Dim candidates As String() = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "haarcascade_frontalface_default.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "models", "haarcascade_frontalface_default.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "models", "haarcascade_frontalface_default.xml")
            }

            For Each candidate As String In candidates
                Dim fullPath = Path.GetFullPath(candidate)
                If File.Exists(fullPath) Then Return fullPath
            Next

            Return ""
        End Function
    End Class
End Namespace
