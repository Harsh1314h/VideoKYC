// webrtc-customer.js
var connection = $.hubConnection();
var kycProxy = connection.createHubProxy('kycHub');
var pc;
var localStream;
var sessionId = document.getElementById('hdnSessionId').value;

// -- SignalR event handlers (incoming from server) --

kycProxy.on('agentJoined', function () {
    console.log("Officer joined call. Initiating WebRTC offer...");
    $('#statusMsg').text('Officer connected. Starting video stream...');
    createOffer(); // Customer always initiates SDP negotiation
});

kycProxy.on('receiveAnswer', function (answer) {
    console.log("SDP Answer received from officer.");
    pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(answer)))
      .catch(e => console.error("Error setting remote description: ", e));
});

kycProxy.on('receiveIceCandidate', function (candidate) {
    console.log("ICE candidate received from officer.");
    pc.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)))
      .catch(e => console.error("Error adding ICE candidate: ", e));
});

// Verification triggers from officer
kycProxy.on('startFaceCapture', function () {
    showInstructionPanel('face');
    startFaceCapture();
});

kycProxy.on('startVoiceCapture', function (phrase) {
    showInstructionPanel('voice');
    startVoiceCapture(phrase);
});

kycProxy.on('startDocumentUpload', function (docType) {
    showInstructionPanel('document');
    startDocumentUpload(docType);
});

// Decision notifications
kycProxy.on('kycApproved', function () {
    showDecisionAlert('Approved', 'Identity verification successful. Your account is active.', 'alert-success');
});

kycProxy.on('kycRejected', function (reason) {
    showDecisionAlert('Rejected', 'Verification failed: ' + reason, 'alert-danger');
});

kycProxy.on('participantDisconnected', function () {
    console.warn("Officer disconnected from call.");
    $('#statusOverlay').removeClass('d-none');
    $('#statusMsg').text("Officer Disconnected. Re-waiting...");
    if (pc) {
        pc.close();
        pc = null;
    }
});

// Initialize connection
connection.start()
    .done(async function () {
        console.log("SignalR Connection established. Session ID: " + sessionId);
        $('#lblDisplaySessionId').text(sessionId);
        await startLocalCamera();
        kycProxy.invoke('joinSession', sessionId, 'customer');
    })
    .fail(function (e) {
        console.error("SignalR Connection failed: ", e);
        $('#statusMsg').text("Connection Failed. Retrying...").addClass("text-danger");
    });

// -- Camera and WebRTC functions --

async function startLocalCamera() {
    try {
        console.log("Requesting webcam access...");
        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        document.getElementById('localVideo').srcObject = localStream;
        $('#statusOverlay').addClass('d-none'); // Hide loading overlay
    } catch (e) {
        console.error("Camera access denied: ", e);
        $('#statusMsg').text("Camera Access Denied! Please enable permissions and refresh.").addClass("text-danger");
    }
}

async function createOffer() {
    pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    // Add local tracks to WebRTC connection
    if (localStream) {
        localStream.getTracks().forEach(track => pc.addTrack(track, localStream));
    } else {
        console.warn("localStream is not initialized; cannot add tracks.");
    }

    // Handle remote video stream
    pc.ontrack = function (event) {
        console.log("Remote track received from officer.");
        document.getElementById('remoteVideo').srcObject = event.streams[0];
    };

    // Handle candidate generation
    pc.onicecandidate = function (event) {
        if (event.candidate) {
            kycProxy.invoke('sendIceCandidate', sessionId, JSON.stringify(event.candidate), 'agent');
        }
    };

    try {
        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        kycProxy.invoke('sendOffer', sessionId, JSON.stringify(offer));
    } catch (e) {
        console.error("Failed to generate SDP Offer: ", e);
    }
}

// UI Panel display switcher
function showInstructionPanel(panelType) {
    $('#pnlDefaultInstruction').addClass('d-none');
    $('#pnlDocumentUpload').addClass('d-none');
    $('#pnlVoiceCapture').addClass('d-none');
    $('#pnlFaceCapture').addClass('d-none');

    if (panelType === 'document') {
        $('#pnlDocumentUpload').removeClass('d-none');
    } else if (panelType === 'voice') {
        $('#pnlVoiceCapture').removeClass('d-none');
    } else if (panelType === 'face') {
        $('#pnlFaceCapture').removeClass('d-none');
    }
}

function showDecisionAlert(title, text, cssClass) {
    // Hide instruction blocks
    $('#pnlDefaultInstruction').addClass('d-none');
    $('#pnlDocumentUpload').addClass('d-none');
    $('#pnlVoiceCapture').addClass('d-none');
    $('#pnlFaceCapture').addClass('d-none');

    // Show result block
    $('#pnlResultAlert')
        .removeClass('d-none alert-success alert-danger')
        .addClass(cssClass)
        .html('<h3 class="fw-bold mb-2">' + title + '</h3><p class="mb-0 fs-7">' + text + '</p>');
}

// -- Mic and Camera Toggle Functionality --
const MIC_ON_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-mic-fill" viewBox="0 0 16 16"><path d="M5 3a3 3 0 0 1 6 0v5a3 3 0 0 1-6 0z"/><path d="M3.5 6.5A.5.5 0 0 1 4 7v1a4 4 0 0 0 8 0V7a.5.5 0 0 1 1 0v1a5 5 0 0 1-4.5 4.975V15h3a.5.5 0 0 1 0 1h-7a.5.5 0 0 1 0-1h3v-2.025A5 5 0 0 1 3 8V7a.5.5 0 0 1 .5-.5"/></svg>`;
const MIC_OFF_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-mic-mute-fill" viewBox="0 0 16 16"><path d="M13 8c0 .564-.09 1.1-.256 1.6l1.24 1.24A5.955 5.955 0 0 0 15 8v-1a.5.5 0 0 0-1 0v1c0 .532-.07 1.05-.2 1.541l-1.077-1.077A4.97 4.97 0 0 0 13 8v-1a.5.5 0 0 0-1 0v1c0 .245-.035.48-.1.7l-1.076-1.077A3.99 3.99 0 0 0 11 3V2H5v1c0 .341.042.671.121.986L4.12 3.011A5.002 5.002 0 0 1 5 1h6a5 5 0 0 1 5 5v1c0 .345-.03.68-.088 1zM8 11h-.008a3 3 0 0 1-2.992-3v-1H4.02l.006.18C4.1 8.8 4.8 11 8 11zm1.614-1.614 2.85 2.85A5.955 5.955 0 0 1 8 13v2H9a1 1 0 0 1 0 2H7a1 1 0 0 1 0-2h1v-2A5.96 5.96 0 0 1 4.542 12.2l1.62-1.62A3.99 3.99 0 0 0 8 10h.015a4.017 4.017 0 0 0 1.599-.614z"/></svg>`;

const CAM_ON_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-camera-video-fill" viewBox="0 0 16 16"><path fill-rule="evenodd" d="M0 5a2 2 0 0 1 2-2h7.5a2 2 0 0 1 1.983 1.738l3.11-1.382A1 1 0 0 1 16 4.269v7.462a1 1 0 0 1-1.406.913l-3.111-1.382A2 2 0 0 1 9.5 13H2a2 2 0 0 1-2-2zm11.5 5.175 3.5 1.556V4.269l-3.5 1.556z"/></svg>`;
const CAM_OFF_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-camera-video-off-fill" viewBox="0 0 16 16"><path fill-rule="evenodd" d="M10.961 12.365a1.99 1.99 0 0 0 .522-1.103l3.11 1.382A1 1 0 0 0 16 11.731V4.269a1 1 0 0 0-1.406-.913l-3.111 1.382A2 2 0 0 0 9.5 3H4.272l6.69 9.365zm-10.114-9A2.001 2.001 0 0 0 0 5v6a2 2 0 0 0 2 2h5.728L1.614 3.365zM2 1h12a1 1 0 0 1 1 1v1h-1V2H2v1H1V2a1 1 0 0 1 1-1z"/></svg>`;

var isMuted = false;
var isCamOff = false;

$(document).ready(function () {
    $('#btnMute').click(function () {
        if (localStream) {
            var audioTracks = localStream.getAudioTracks();
            if (audioTracks.length > 0) {
                isMuted = !isMuted;
                audioTracks[0].enabled = !isMuted;
                
                if (isMuted) {
                    $(this).removeClass('btn-outline-light').addClass('btn-danger').attr('title', 'Unmute Microphone').html(MIC_OFF_SVG);
                } else {
                    $(this).removeClass('btn-danger').addClass('btn-outline-light').attr('title', 'Mute Microphone').html(MIC_ON_SVG);
                }
                console.log("Microphone " + (isMuted ? "muted" : "unmuted"));
            }
        }
    });

    $('#btnCamOff').click(function () {
        if (localStream) {
            var videoTracks = localStream.getVideoTracks();
            if (videoTracks.length > 0) {
                isCamOff = !isCamOff;
                videoTracks[0].enabled = !isCamOff;
                
                if (isCamOff) {
                    $(this).removeClass('btn-outline-light').addClass('btn-danger').attr('title', 'Turn Camera On').html(CAM_OFF_SVG);
                } else {
                    $(this).removeClass('btn-danger').addClass('btn-outline-light').attr('title', 'Turn Camera Off').html(CAM_ON_SVG);
                }
                console.log("Camera " + (isCamOff ? "disabled" : "enabled"));
            }
        }
    });
});
