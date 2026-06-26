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

async function startFaceCapture() {
    $('#instructionMsg').text('Analyzing face... Please look directly at the camera.');

    const video = document.getElementById('localVideo');
    const canvas = document.getElementById('captureCanvas');
    const context = canvas.getContext('2d');

    // Draw video frame to canvas
    context.drawImage(video, 0, 0, canvas.width, canvas.height);

    var clientMatched = false;

    // 1. Run client-side biometrics comparison if models are loaded
    if (modelsLoaded) {
        try {
            console.log("Running client-side face-api.js biometric check...");
            
            // Detect face descriptor on live webcam canvas
            const liveDetection = await faceapi.detectSingleFace(canvas)
                .withFaceLandmarks()
                .withFaceDescriptor();

            // Detect face descriptor on document photo (use imgFrontPreview)
            const docImage = document.getElementById('imgFrontPreview');
            
            if (docImage && docImage.src && !docImage.src.endsWith('#')) {
                const docDetection = await faceapi.detectSingleFace(docImage)
                    .withFaceLandmarks()
                    .withFaceDescriptor();

                if (liveDetection && docDetection) {
                    // Compute Euclidean distance (0.0 = identical, 1.0 = completely different)
                    const distance = faceapi.euclideanDistance(liveDetection.descriptor, docDetection.descriptor);
                    
                    // Convert to percentage score
                    const score = Math.round((1 - Math.min(distance, 1)) * 100);
                    const verified = distance < 0.50; // threshold is 0.50

                    console.log("Client Face match distance: " + distance + " | Score: " + score + "%");

                    // Send client result back to officer
                    sendVerificationResult('face', {
                        verified: verified,
                        score: score,
                        distance: distance
                    });

                    $('#instructionMsg').text(verified ? 'Face Verified!' : 'Face Mismatch. Align face.');
                    clientMatched = true;
                } else {
                    console.warn("Face not detected in webcam or document image on client side.");
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

        fetch('/Handlers/VerifyFace.ashx', {
            method: 'POST',
            body: fd
        })
        .then(response => response.json())
        .then(data => {
            console.log("Server Face Verification result: ", data);
            
            // If client-side did not verify, use server result
            if (!clientMatched) {
                var serverScore = Math.round(data.serverScore);
                var verified = data.serverScore >= 45.0;
                
                sendVerificationResult('face', {
                    verified: verified,
                    score: serverScore,
                    distance: 1.0 - (data.serverScore / 100)
                });
                
                $('#instructionMsg').text(verified ? 'Face Verified Successfully!' : 'Face Match Failed. Check lighting.');
            }
        })
        .catch(err => {
            console.error("Server Face Verification failed: ", err);
            if (!clientMatched) {
                $('#instructionMsg').text('Face verification failed. Check lighting.');
            }
        });
    }, 'image/jpeg');
}

function sendVerificationResult(type, result) {
    kycProxy.invoke('sendVerificationResult', sessionId, type, JSON.stringify(result));
}
