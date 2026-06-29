// face-verification.js
var modelsLoaded = false;

// Proactively load face models
async function loadFaceModels() {
    try {
        console.log("Loading face-api.js models...");
        // Use local models folder
        await faceapi.nets.ssdMobilenetv1.loadFromUri('/models');
        await faceapi.nets.faceLandmark68Net.loadFromUri('/models');
        await faceapi.nets.faceRecognitionNet.loadFromUri('/models');
        modelsLoaded = true;
        console.log("face-api.js models loaded successfully.");
    } catch (e) {
        console.error("Error loading face-api.js models: ", e);
        // We will fall back fully to server-side comparison
    }
}

// Call on load
loadFaceModels();

// Helper to check face detection at different rotations (0, 90, 180, 270)
async function detectFaceWithRotations(imageElement) {
    // 1. Try first with 0 degrees (no rotation)
    let detection = await faceapi.detectSingleFace(imageElement, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.15 }))
        .withFaceLandmarks()
        .withFaceDescriptor();
    if (detection) {
        console.log("Face detected on document at 0 degrees rotation.");
        return detection;
    }

    // 2. If it fails, let's create a temporary canvas to try 90, 180, and 270 degrees rotation
    const tempCanvas = document.createElement('canvas');
    const ctx = tempCanvas.getContext('2d');
    const rotations = [90, 180, 270];

    for (let r of rotations) {
        if (r === 90 || r === 270) {
            tempCanvas.width = imageElement.naturalHeight || imageElement.height;
            tempCanvas.height = imageElement.naturalWidth || imageElement.width;
        } else {
            tempCanvas.width = imageElement.naturalWidth || imageElement.width;
            tempCanvas.height = imageElement.naturalHeight || imageElement.height;
        }

        ctx.clearRect(0, 0, tempCanvas.width, tempCanvas.height);
        ctx.save();
        ctx.translate(tempCanvas.width / 2, tempCanvas.height / 2);
        ctx.rotate((r * Math.PI) / 180);
        ctx.drawImage(imageElement, -imageElement.naturalWidth / 2, -imageElement.naturalHeight / 2);
        ctx.restore();

        console.log("Checking face-api on document at " + r + " degrees rotation...");
        detection = await faceapi.detectSingleFace(tempCanvas, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.15 }))
            .withFaceLandmarks()
            .withFaceDescriptor();

        if (detection) {
            console.log("Face successfully detected on document at " + r + " degrees rotation!");
            // Rotate the DOM preview image so the officer and customer see it upright!
            $(imageElement).css('transform', 'rotate(' + r + 'deg)');
            return detection;
        }
    }

    console.warn("Face could not be detected on document image in any rotation.");
    return null;
}

async function startFaceCapture() {
    $('#instructionMsg').text('Analyzing face... Please look directly at the camera.');

    const video = document.getElementById('localVideo');
    const canvas = document.getElementById('captureCanvas');
    const context = canvas.getContext('2d');

    // Set canvas dimensions to match video stream resolution for maximum face feature quality
    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;

    // Draw video frame to canvas
    context.drawImage(video, 0, 0, canvas.width, canvas.height);

    var clientMatched = false;
    var clientResult = null;

    // 1. Run client-side biometrics comparison if models are loaded
    if (modelsLoaded) {
        try {
            console.log("Running client-side face-api.js biometric check at " + canvas.width + "x" + canvas.height + "...");
            
            // Detect face descriptor on live webcam canvas (using lower confidence for better recall in sub-optimal environments)
            const liveDetection = await faceapi.detectSingleFace(canvas, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.15 }))
                .withFaceLandmarks()
                .withFaceDescriptor();

            // Detect face descriptor on document photo (use imgFrontPreview)
            const docImage = document.getElementById('imgFrontPreview');
            
            if (docImage && docImage.src && !docImage.src.endsWith('#')) {
                // Ensure document is loaded
                if (!docImage.complete || docImage.naturalWidth === 0) {
                    console.warn("Document image is not fully loaded in DOM yet.");
                }

                // Detect face using auto-rotation helper
                const docDetection = await detectFaceWithRotations(docImage);

                if (liveDetection && docDetection) {
                    // Compute Euclidean distance (0.0 = identical, 1.0 = completely different)
                    const distance = faceapi.euclideanDistance(liveDetection.descriptor, docDetection.descriptor);
                    
                    // Standard robust threshold for face-api.js is 0.60
                    const faceApiThreshold = 0.60;
                    const score = Math.round(Math.max(0, Math.min(100, 100 - ((distance / faceApiThreshold) * 50))));
                    const verified = score >= 50; 

                    console.log("Client Face match distance: " + distance + " | Score: " + score + "% | Verified: " + verified);

                    clientResult = {
                        verified: verified,
                        score: score,
                        distance: distance
                    };

                    $('#instructionMsg').text(verified ? 'Face Verified Successfully!' : 'Face Mismatch. Please realign face.');
                    clientMatched = true;
                } else {
                    if (!liveDetection) {
                        console.warn("Face NOT detected on live webcam feed.");
                    }
                    if (!docDetection) {
                        console.warn("Face NOT detected on document image preview.");
                    }
                }
            } else {
                console.warn("No front document photo preview available on client side.");
            }
        } catch (e) {
            console.error("Client-side face-api matching crashed: ", e);
        }
    }

    // 2. Upload frame to server for backup verification
    canvas.toBlob(function (blob) {
        var fd = new FormData();
        fd.append('frame', blob, 'live_frame.jpg');
        fd.append('sessionId', sessionId);
        fd.append('clientScore', clientResult ? clientResult.score.toString() : '0');
        fd.append('clientVerified', clientResult ? clientResult.verified.toString() : 'false');
        fd.append('clientDistance', clientResult ? clientResult.distance.toString() : '');

        fetch('/Handlers/VerifyFace.ashx', {
            method: 'POST',
            body: fd
        })
        .then(response => response.json())
        .then(data => {
            console.log("Server Face Verification result: ", data);
            
            var finalScore = Math.round(data.score != null ? data.score : data.serverScore);
            var verified = data.verified;
            
            sendVerificationResult('face', {
                verified: verified,
                score: finalScore,
                distance: clientResult ? clientResult.distance : (1.0 - (finalScore / 100)),
                clientScore: data.clientScore,
                serverScore: data.serverScore
            });
            
            $('#instructionMsg').text(verified ? 'Face Verified Successfully!' : (data.errorMsg || 'Face Match Failed. Please realign face and try again.'));
        })
        .catch(err => {
            console.error("Server Face Verification failed: ", err);
            $('#instructionMsg').text('Face verification could not be saved. Please try again.');
        });
    }, 'image/jpeg');
}

function sendVerificationResult(type, result) {
    kycProxy.invoke('sendVerificationResult', sessionId, type, JSON.stringify(result));
}
