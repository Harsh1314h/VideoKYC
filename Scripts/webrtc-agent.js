// webrtc-agent.js
var connection = $.hubConnection();
var kycProxy = connection.createHubProxy('kycHub');
var pc;
var localStream;
var sessionId = getQueryParam('sid');

// Verification state flags
var docVerified = typeof initialDocVerified !== 'undefined' ? initialDocVerified : false;
var faceVerified = typeof initialFaceVerified !== 'undefined' ? initialFaceVerified : false;
var voiceVerified = typeof initialVoiceVerified !== 'undefined' ? initialVoiceVerified : false;

// -- SignalR event handlers (incoming from server) --

var remoteCandidatesQueue = [];

function processQueuedCandidates() {
    console.log("Processing " + remoteCandidatesQueue.length + " queued ICE candidates...");
    while (remoteCandidatesQueue.length > 0) {
        var candidate = remoteCandidatesQueue.shift();
        if (pc && pc.remoteDescription && pc.remoteDescription.type) {
            pc.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)))
              .catch(e => console.error("Error adding queued ICE candidate: ", e));
        }
    }
}

kycProxy.on('receiveOffer', async function (offer) {
    console.log("SDP Offer received from customer. Creating RTC Connection...");
    $('#statusMsg').text('Customer connected. Opening stream...');
    remoteCandidatesQueue = [];
    
    pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    // Add local tracks
    if (localStream) {
        localStream.getTracks().forEach(track => pc.addTrack(track, localStream));
    } else {
        console.warn("localStream is not initialized; cannot add tracks.");
    }

    // Handle remote tracks
    pc.ontrack = function (e) {
        console.log("Remote track received from customer.");
        document.getElementById('customerVideo').srcObject = e.streams[0];
        $('#statusOverlay').addClass('d-none'); // Hide loading overlay
    };

    // Handle connection state changes to hide overlay when connected
    pc.onconnectionstatechange = function() {
        console.log("WebRTC Connection State: " + pc.connectionState);
        if (pc.connectionState === 'connected') {
            $('#statusOverlay').addClass('d-none');
        }
    };

    pc.oniceconnectionstatechange = function() {
        console.log("WebRTC ICE Connection State: " + pc.iceConnectionState);
        if (pc.iceConnectionState === 'connected' || pc.iceConnectionState === 'completed') {
            $('#statusOverlay').addClass('d-none');
        }
    };

    pc.onicecandidate = function (e) {
        if (e.candidate) {
            kycProxy.invoke('sendIceCandidate', sessionId, JSON.stringify(e.candidate), 'customer');
        }
    };

    try {
        await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
        processQueuedCandidates();
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        kycProxy.invoke('sendAnswer', sessionId, JSON.stringify(answer));
    } catch (e) {
        console.error("Failed to create SDP Answer: ", e);
    }
});

kycProxy.on('receiveIceCandidate', function (candidate) {
    console.log("ICE candidate received from customer.");
    if (pc && pc.remoteDescription && pc.remoteDescription.type) {
        pc.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)))
          .catch(e => console.error("Error adding candidate: ", e));
    } else {
        console.log("Queueing early ICE candidate from customer.");
        remoteCandidatesQueue.push(candidate);
    }
});

kycProxy.on('receiveVerificationResult', function (type, json) {
    console.log("Verification Result received: " + type, json);
    updateVerificationPanel(type, JSON.parse(json));
});

kycProxy.on('showApprovalError', function (errorMsg) {
    alert(errorMsg);
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
    .done(async function () {
        console.log("SignalR Connection established. Session: " + sessionId);
        $('#lblTitleSessionId').text(sessionId);
        await startLocalCamera();
        kycProxy.invoke('joinSession', sessionId, 'agent');
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
        $('#statusSpinner').addClass('d-none');
        $('#statusMsg').text("Camera/Microphone Access Denied!").addClass("text-danger");
        $('#statusDetails').html(
            "To grant access: Click the <strong>padlock icon (🔒)</strong> next to the website URL in your browser's address bar, " +
            "change the <strong>Camera</strong> and <strong>Microphone</strong> settings to <strong>Allow</strong>, and then refresh the page."
        ).removeClass("text-secondary-light").addClass("text-warning fs-7 mt-2 d-block");
        $('#statusOverlay').removeClass('d-none');
        alert("Camera/Microphone Access Denied!\n\nTo conduct KYC verification sessions, please click the lock icon (🔒) in your browser's address bar, allow Camera/Microphone, and refresh the page.");
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

function triggerVoiceChallenge() {
    var val = $('#ddlVoicePhrase').val();
    var phrase = "";
    if (val === 'name') {
        var customerName = $('#lblCustName').text().trim();
        phrase = "My name is " + customerName;
    } else {
        phrase = "I authorize this KYC process";
    }
    console.log("Triggering voice challenge-phrase verification: " + phrase);
    $('#voiceStatus').text('Prompted...').removeClass('bg-success bg-danger bg-secondary').addClass('bg-warning text-dark');
    kycProxy.invoke('triggerVoiceVerification', sessionId, phrase);
}

// Decision functions
function approveKyc() {
    if (!docVerified || !faceVerified || !voiceVerified) {
        alert("Cannot Approve KYC: All checks (Document OCR, Biometric Face Match, and Voice Verification) must be successfully verified before approval.");
        return;
    }
    if (confirm("Are you sure you want to APPROVE this customer's KYC?")) {
        kycProxy.invoke('approveKyc', sessionId)
            .done(function (success) {
                if (success) {
                    alert("KYC Session Approved successfully.");
                    window.location.href = "Queue.aspx";
                }
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
            .done(function (success) {
                if (success) {
                    alert("KYC Session Rejected.");
                    window.location.href = "Queue.aspx";
                }
            });
    }
}

// -- Panel UI Update functions --

function updateVerificationPanel(type, result) {
    if (type === 'face') {
        var score = result.score;
        var verified = result.verified;
        faceVerified = verified;
        
        $('#faceScore').text(score + '%');
        $('#faceStatus').text(verified ? 'Match ✓' : 'Mismatch ✗')
            .removeClass('bg-secondary bg-warning bg-danger bg-success')
            .addClass(verified ? 'bg-success text-dark' : 'bg-danger text-white');
    }
    else if (type === 'voice') {
        var finalScore = result.finalScore;
        var verified = result.verified;
        voiceVerified = verified;
        var spokenText = result.spokenText;
        
        $('#voiceScore').text(finalScore + '%');
        $('#voiceSpoken').text(spokenText);
        $('#voiceStatus').text(verified ? 'Voice Verified' : 'Voice Not Verified')
            .removeClass('bg-secondary bg-warning bg-danger bg-success')
            .addClass(verified ? 'bg-success text-dark' : 'bg-danger text-white');
    }
    else if (type === 'document') {
        var isVerified = result.IsVerified;
        docVerified = isVerified;
        var fields = result.Fields;
        
        $('#docStatus').text(isVerified ? 'Extracted ✓' : 'Failed ✗')
            .removeClass('bg-secondary bg-warning bg-danger bg-success')
            .addClass(isVerified ? 'bg-success text-dark' : 'bg-danger text-white');
            
        // Build image previews and fields table
        var paths = result.ImagePath ? result.ImagePath.split(',') : [];
        var html = '';
        
        if (paths.length > 0) {
            html += '<div class="row g-2 mb-3">';
            // Front side
            if (paths[0]) {
                var frontSrc = paths[0].replace('~', '') + '?t=' + new Date().getTime();
                html += '<div class="col text-center">';
                html += '  <span class="text-secondary-light fs-9 d-block mb-1 fw-semibold">Front Side</span>';
                html += '  <img src="' + frontSrc + '" class="img-fluid rounded border border-secondary-light border-opacity-20" style="max-height: 120px; object-fit: contain; cursor: pointer; background: rgba(0,0,0,0.2);" onclick="window.open(\'' + frontSrc + '\', \'_blank\')" title="Click to view full size" />';
                html += '</div>';
            }
            // Back side
            if (paths.length > 1 && paths[1]) {
                var backSrc = paths[1].replace('~', '') + '?t=' + new Date().getTime();
                html += '<div class="col text-center">';
                html += '  <span class="text-secondary-light fs-9 d-block mb-1 fw-semibold">Back Side</span>';
                html += '  <img src="' + backSrc + '" class="img-fluid rounded border border-secondary-light border-opacity-20" style="max-height: 120px; object-fit: contain; cursor: pointer; background: rgba(0,0,0,0.2);" onclick="window.open(\'' + backSrc + '\', \'_blank\')" title="Click to view full size" />';
                html += '</div>';
            }
            html += '</div>';
            html += '<span class="text-secondary-light fs-9 d-block mt-1 mb-3 text-center">Click an image to view full size</span>';
        }
        
        html += '<table class="table table-sm table-dark table-striped mb-0">';
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

// -- Mic and Camera Toggle Functionality --
const MIC_ON_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-mic-fill" viewBox="0 0 16 16"><path d="M5 3a3 3 0 0 1 6 0v5a3 3 0 0 1-6 0z"/><path d="M3.5 6.5A.5.5 0 0 1 4 7v1a4 4 0 0 0 8 0V7a.5.5 0 0 1 1 0v1a5 5 0 0 1-4.5 4.975V15h3a.5.5 0 0 1 0 1h-7a.5.5 0 0 1 0-1h3v-2.025A5 5 0 0 1 3 8V7a.5.5 0 0 1 .5-.5"/></svg>`;
const MIC_OFF_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-mic-mute-fill" viewBox="0 0 16 16"><path d="M13 8c0 .564-.09 1.1-.256 1.6l1.24 1.24A5.955 5.955 0 0 0 15 8v-1a.5.5 0 0 0-1 0v1c0 .532-.07 1.05-.2 1.541l-1.077-1.077A4.97 4.97 0 0 0 13 8v-1a.5.5 0 0 0-1 0v1c0 .245-.035.48-.1.7l-1.076-1.077A3.99 3.99 0 0 0 11 3V2H5v1c0 .341.042.671.121.986L4.12 3.011A5.002 5.002 0 0 1 5 1h6a5 5 0 0 1 5 5v1c0 .345-.03.68-.088 1zM8 11h-.008a3 3 0 0 1-2.992-3v-1H4.02l.006.18C4.1 8.8 4.8 11 8 11zm1.614-1.614 2.85 2.85A5.955 5.955 0 0 1 8 13v2H9a1 1 0 0 1 0 2H7a1 1 0 0 1 0-2h1v-2A5.96 5.96 0 0 1 4.542 12.2l1.62-1.62A3.99 3.99 0 0 0 8 10h.015a4.017 4.017 0 0 0 1.599-.614z"/></svg>`;

const CAM_ON_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-camera-video-fill" viewBox="0 0 16 16"><path fill-rule="evenodd" d="M0 5a2 2 0 0 1 2-2h7.5a2 2 0 0 1 1.983 1.738l3.11-1.382A1 1 0 0 1 16 4.269v7.462a1 1 0 0 1-1.406.913l-3.111-1.382A2 2 0 0 1 9.5 13H2a2 2 0 0 1-2-2zm11.5 5.175 3.5 1.556V4.269l-3.5 1.556z"/></svg>`;
const CAM_OFF_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-camera-video-off-fill" viewBox="0 0 16 16"><path fill-rule="evenodd" d="M10.961 12.365a1.99 1.99 0 0 0 .522-1.103l3.11 1.382A1 1 0 0 0 16 11.731V4.269a1 1 0 0 0-1.406-.913l-3.111 1.382A2 2 0 0 0 9.5 3H4.272l6.69 9.365zm-10.114-9A2.001 2.001 0 0 0 0 5v6a2 2 0 0 0 2 2h5.728L1.614 3.365zM2 1h12a1 1 0 0 1 1 1v1h-1V2H2v1H1V2a1 1 0 0 1 1-1z"/></svg>`;

var isMuted = false;
var isCamOff = false;

$(document).ready(function () {
    // Populate dynamic name in voice phrase option
    var customerName = $('#lblCustName').text().trim();
    if (customerName && customerName !== '--') {
        $('#optVoiceName').text('My name is ' + customerName);
    }

    // Initialize UI badges from initial verification state
    if (docVerified) {
        $('#docStatus').text('Extracted ✓')
            .removeClass('bg-secondary bg-warning bg-danger')
            .addClass('bg-success text-dark');
        $('#docExtractedData').html('<div class="text-success fs-8">Verified previously. Reload/Re-upload if needed.</div>');
    }
    if (faceVerified) {
        $('#faceStatus').text('Match ✓')
            .removeClass('bg-secondary bg-warning bg-danger')
            .addClass('bg-success text-dark');
    }
    if (voiceVerified) {
        $('#voiceStatus').text('Voice Verified')
            .removeClass('bg-secondary bg-warning bg-danger')
            .addClass('bg-success text-dark');
    }

    $('#btnMute').click(function (e) {
        e.preventDefault();
        alert("Microphone cannot be turned off during an active KYC session.");
        return false;
    });

    $('#btnCamOff').click(function (e) {
        e.preventDefault();
        alert("Camera cannot be turned off during an active KYC session.");
        return false;
    });

    // Periodic heartbeat to keep session active in DB
    setInterval(function () {
        if (sessionId) {
            kycProxy.invoke('keepAlive', sessionId);
        }
    }, 5000);
});
