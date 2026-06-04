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
