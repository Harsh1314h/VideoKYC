// webrtc-agent.js
var connection = $.hubConnection();
var kycProxy = connection.createHubProxy('kycHub');
var pc;
var localStream;
var sessionId = getQueryParam('sid');

// -- SignalR event handlers (incoming from server) --

kycProxy.on('receiveOffer', async function (offer) {
    console.log("SDP Offer received from customer. Creating RTC Connection...");
    $('#statusMsg').text('Customer connected. Opening stream...');
    
    pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    // Add local tracks
    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));

    // Handle remote tracks
    pc.ontrack = function (e) {
        console.log("Remote track received from customer.");
        document.getElementById('customerVideo').srcObject = e.streams[0];
        $('#statusOverlay').addClass('d-none'); // Hide loading overlay
    };

    pc.onicecandidate = function (e) {
        if (e.candidate) {
            kycProxy.invoke('sendIceCandidate', sessionId, JSON.stringify(e.candidate), 'customer');
        }
    };

    try {
        await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        kycProxy.invoke('sendAnswer', sessionId, JSON.stringify(answer));
    } catch (e) {
        console.error("Failed to create SDP Answer: ", e);
    }
});

kycProxy.on('receiveIceCandidate', function (candidate) {
    console.log("ICE candidate received from customer.");
    pc.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)))
      .catch(e => console.error("Error adding candidate: ", e));
});

kycProxy.on('receiveVerificationResult', function (type, json) {
    console.log("Verification Result received: " + type, json);
    updateVerificationPanel(type, JSON.parse(json));
});

kycProxy.on('participantDisconnected', function () {
    console.warn("Customer disconnected from call.");
    $('#statusOverlay').removeClass('d-none');
    $('#statusMsg').text("Awaiting Customer Reconnection...");
    if (pc) {
        pc.close();
        pc = null;
    }
});

// Initialize connection
connection.start()
    .done(function () {
        console.log("SignalR Connection established. Session: " + sessionId);
        $('#lblTitleSessionId').text(sessionId);
        kycProxy.invoke('joinSession', sessionId, 'agent');
        startLocalCamera();
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
        document.getElementById('agentVideo').srcObject = localStream;
    } catch (e) {
        console.error("Camera access denied: ", e);
        alert("Camera Access Denied! Please allow camera permissions to conduct calls.");
    }
}

// -- Verification Panel Controls --

function triggerDocument(type) {
    console.log("Triggering document upload request: " + type);
    $('#docStatus').text('Requested...').removeClass('bg-success bg-danger').addClass('bg-warning text-dark');
    kycProxy.invoke('triggerDocumentUpload', sessionId, type);
}

function triggerFace() {
    console.log("Triggering face comparison...");
    $('#faceStatus').text('Comparing...').removeClass('bg-success bg-danger').addClass('bg-warning text-dark');
    kycProxy.invoke('triggerFaceVerification', sessionId);
}

function triggerVoice() {
    console.log("Triggering voice challenge-phrase verification...");
    $('#voiceStatus').text('Prompted...').removeClass('bg-success bg-danger').addClass('bg-warning text-dark');
    kycProxy.invoke('triggerVoiceVerification', sessionId);
}

// Decision functions
function approveKyc() {
    if (confirm("Are you sure you want to APPROVE this customer's KYC?")) {
        kycProxy.invoke('approveKyc', sessionId)
            .done(function () {
                alert("KYC Session Approved successfully.");
                window.location.href = "Queue.aspx";
            });
    }
}

function rejectKyc() {
    var reason = document.getElementById('rejectionReason').value.trim();
    if (!reason) {
        alert("Please enter a rejection reason before rejecting the KYC.");
        document.getElementById('rejectionReason').focus();
        return;
    }
    
    if (confirm("Are you sure you want to REJECT this customer's KYC?")) {
        kycProxy.invoke('rejectKyc', sessionId, reason)
            .done(function () {
                alert("KYC Session Rejected.");
                window.location.href = "Queue.aspx";
            });
    }
}

// -- Panel UI Update functions --

function updateVerificationPanel(type, result) {
    if (type === 'face') {
        var score = result.score;
        var verified = result.verified;
        
        $('#faceScore').text(score + '%');
        $('#faceStatus').text(verified ? 'Match ✓' : 'Mismatch ✗')
            .removeClass('bg-secondary bg-warning bg-danger bg-success')
            .addClass(verified ? 'bg-success text-dark' : 'bg-danger text-white');
    }
    else if (type === 'voice') {
        var finalScore = result.finalScore;
        var verified = result.verified;
        var spokenText = result.spokenText;
        
        $('#voiceScore').text(finalScore + '%');
        $('#voiceSpoken').text(spokenText);
        $('#voiceStatus').text(verified ? 'Match ✓' : 'Mismatch ✗')
            .removeClass('bg-secondary bg-warning bg-danger bg-success')
            .addClass(verified ? 'bg-success text-dark' : 'bg-danger text-white');
    }
    else if (type === 'document') {
        var isVerified = result.IsVerified;
        var fields = result.Fields;
        
        $('#docStatus').text(isVerified ? 'Extracted ✓' : 'Failed ✗')
            .removeClass('bg-secondary bg-warning bg-danger bg-success')
            .addClass(isVerified ? 'bg-success text-dark' : 'bg-danger text-white');
            
        // Build table
        var html = '<table class="table table-sm table-dark table-striped mb-0">';
        for (var key in fields) {
            html += '<tr><td class="text-secondary-light fw-medium">' + key + '</td><td>' + fields[key] + '</td></tr>';
        }
        html += '</table>';
        $('#docExtractedData').html(html);
    }
}

// Helper: Query parameter parser
function getQueryParam(name) {
    name = name.replace(/[\[]/, '\\[').replace(/[\]]/, '\\]');
    var regex = new RegExp('[\\?&]' + name + '=([^&#]*)');
    var results = regex.exec(location.search);
    return results === null ? '' : decodeURIComponent(results[1].replace(/\+/g, ' '));
}
