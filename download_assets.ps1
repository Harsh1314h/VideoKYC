# download_assets.ps1
$ProgressPreference = 'SilentlyContinue'

# Define target paths
$tessDir = "C:\videokyc\App_Data\tessdata"
$modelsDir = "C:\videokyc\models"

# Create directories if they do not exist
if (!(Test-Path $tessDir)) {
    New-Item -ItemType Directory -Force -Path $tessDir | Out-Null
    Write-Host "Created Tesseract directory at $tessDir"
}
if (!(Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Force -Path $modelsDir | Out-Null
    Write-Host "Created Face API models directory at $modelsDir"
}

# Tesseract trained data urls
$tessFiles = @{
    "eng.traineddata" = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata"
    "hin.traineddata" = "https://github.com/tesseract-ocr/tessdata/raw/main/hin.traineddata"
}

# Face API weights urls
$faceFiles = @{
    "ssd_mobilenetv1_model-weights_manifest.json" = "https://github.com/justadudewhohacks/face-api.js/raw/master/weights/ssd_mobilenetv1_model-weights_manifest.json"
    "ssd_mobilenetv1_model-shard1" = "https://github.com/justadudewhohacks/face-api.js/raw/master/weights/ssd_mobilenetv1_model-shard1"
    "face_landmark_68_model-weights_manifest.json" = "https://github.com/justadudewhohacks/face-api.js/raw/master/weights/face_landmark_68_model-weights_manifest.json"
    "face_landmark_68_model-shard1" = "https://github.com/justadudewhohacks/face-api.js/raw/master/weights/face_landmark_68_model-shard1"
    "face_recognition_model-weights_manifest.json" = "https://github.com/justadudewhohacks/face-api.js/raw/master/weights/face_recognition_model-weights_manifest.json"
    "face_recognition_model-shard1" = "https://github.com/justadudewhohacks/face-api.js/raw/master/weights/face_recognition_model-shard1"
}

# Download Tesseract models
foreach ($fileName in $tessFiles.Keys) {
    $targetPath = Join-Path $tessDir $fileName
    $url = $tessFiles[$fileName]
    if (!(Test-Path $targetPath)) {
        Write-Host "Downloading $fileName to $tessDir..."
        Invoke-WebRequest -Uri $url -OutFile $targetPath -UserAgent "Mozilla/5.0"
        Write-Host "Finished downloading $fileName"
    } else {
        Write-Host "$fileName already exists, skipping."
    }
}

# Download Face API weights
foreach ($fileName in $faceFiles.Keys) {
    $targetPath = Join-Path $modelsDir $fileName
    $url = $faceFiles[$fileName]
    if (!(Test-Path $targetPath)) {
        Write-Host "Downloading $fileName to $modelsDir..."
        Invoke-WebRequest -Uri $url -OutFile $targetPath -UserAgent "Mozilla/5.0"
        Write-Host "Finished downloading $fileName"
    } else {
        Write-Host "$fileName already exists, skipping."
    }
}

Write-Host "All assets downloaded and verified successfully."
