// voice-verification.js
var mediaRecorder;
var audioChunks = [];
var recognition;
var currentChallengePhrase = "";

function startVoiceCapture(phrase) {
    console.log("Displaying voice verification challenge. Phrase: " + phrase);
    currentChallengePhrase = phrase;
    
    // Set UI prompt
    $('#voicePhrase').text(phrase);
    $('#spokenTextDisplay').text('--');
    $('#voiceInstructions').addClass('d-none');
    $('#btnRecordVoice').text('Start Recording')
        .addClass('btn-primary-gradient')
        .removeClass('btn-danger')
        .prop('disabled', false);
}

function recordAndAnalyzeVoice(phrase) {
    console.log("Starting recording for phrase: " + phrase);
    $('#voiceInstructions').removeClass('d-none');
    $('#btnRecordVoice').text('Recording...').addClass('btn-danger').removeClass('btn-primary-gradient').prop('disabled', true);

    audioChunks = [];
    window._pendingVoice = { spokenText: "", textScore: 0.0 };

    // -- Layer 1: Client Web Speech API for Speech-to-Text --
    var SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (SpeechRecognition) {
        recognition = new SpeechRecognition();
        recognition.lang = 'en-IN';
        recognition.continuous = false;
        recognition.interimResults = false;

        recognition.onresult = function (event) {
            var spokenText = event.results[0][0].transcript.toLowerCase().trim();
            var challenge = phrase.toLowerCase().trim();
            
            // Calculate similarity score
            var textScore = levenshteinSimilarity(spokenText, challenge) * 100;
            
            console.log("Client Speech Recognized: '" + spokenText + "' | Similarity: " + textScore + "%");
            
            $('#spokenTextDisplay').text(spokenText);
            window._pendingVoice = { spokenText: spokenText, textScore: textScore };
        };

        recognition.onerror = function(event) {
            console.error("Speech Recognition Error: ", event.error);
        };

        recognition.start();
    } else {
        console.warn("Speech Recognition not supported on this browser.");
        // Fallback representation
        window._pendingVoice = { spokenText: "Speech-to-text not supported in browser", textScore: 100.0 };
    }

    // -- Layer 2: MediaRecorder to capture audio --
    navigator.mediaDevices.getUserMedia({ audio: true })
        .then(stream => {
            mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });
            mediaRecorder.ondataavailable = e => {
                if (e.data.size > 0) audioChunks.push(e.data);
            };
            mediaRecorder.onstop = () => {
                uploadVoiceClip(phrase);
                // Stop microphone tracks
                stream.getTracks().forEach(t => t.stop());
            };
            
            mediaRecorder.start();
            
            // Auto stop recording after 6 seconds
            setTimeout(function () {
                if (mediaRecorder && mediaRecorder.state === 'recording') {
                    mediaRecorder.stop();
                }
                if (recognition) {
                    recognition.stop();
                }
            }, 6000);
        })
        .catch(err => {
            console.error("Microphone access failed: ", err);
            $('#voiceInstructions').addClass('d-none');
            $('#btnRecordVoice').text('Start Recording').removeClass('btn-danger').addClass('btn-primary-gradient').prop('disabled', false);
            sendVerificationResult('voice', {
                spokenText: "Microphone Access Denied",
                textScore: 0,
                voiceScore: 0,
                finalScore: 0,
                verified: false
            });
        });
}

$(document).ready(function () {
    $('#btnRecordVoice').click(function () {
        if (currentChallengePhrase) {
            recordAndAnalyzeVoice(currentChallengePhrase);
        } else {
            console.warn("No active challenge phrase to record.");
        }
    });
});

function uploadVoiceClip(phrase) {
    var voice = window._pendingVoice;
    var blob = new Blob(audioChunks, { type: 'audio/webm' });
    var fd = new FormData();
    fd.append('audio', blob, 'voice.webm');
    fd.append('sessionId', sessionId);
    fd.append('phrase', phrase);
    fd.append('spokenText', voice.spokenText);
    fd.append('textScore', voice.textScore);

    console.log("Uploading voice clip to server handler...");

    fetch('/Handlers/AnalyzeVoice.ashx', {
        method: 'POST',
        body: fd
    })
    .then(r => r.json())
    .then(serverResult => {
        console.log("Server Voice Analysis results: ", serverResult);
        
        var final = serverResult.finalScore;
        var verified = serverResult.verified;

        // Reset UI buttons
        $('#voiceInstructions').addClass('d-none');
        $('#btnRecordVoice').text('Start Recording').removeClass('btn-danger').addClass('btn-primary-gradient').prop('disabled', false);

        // Send combined voice verification results back to officer
        sendVerificationResult('voice', {
            spokenText: voice.spokenText,
            textScore: Math.round(voice.textScore),
            voiceScore: Math.round(serverResult.mfccScore),
            finalScore: Math.round(final),
            verified: verified
        });
    })
    .catch(err => {
        console.error("Voice Upload/Analysis failed: ", err);
        // Reset UI
        $('#voiceInstructions').addClass('d-none');
        $('#btnRecordVoice').text('Start Recording').removeClass('btn-danger').addClass('btn-primary-gradient').prop('disabled', false);
    });
}

// Levenshtein Similarity calculation: returns 0.0 (totally different) to 1.0 (identical)
function levenshteinSimilarity(a, b) {
    if (a.length === 0) return b.length === 0 ? 1.0 : 0.0;
    if (b.length === 0) return 0.0;

    var matrix = [];

    // Increment along the first column of each row
    for (var i = 0; i <= b.length; i++) {
        matrix[i] = [i];
    }

    // Increment each column in the first row
    for (var j = 0; j <= a.length; j++) {
        matrix[0][j] = j;
    }

    // Fill in the rest of the matrix
    for (var i = 1; i <= b.length; i++) {
        for (var j = 1; j <= a.length; j++) {
            if (b.charAt(i - 1) === a.charAt(j - 1)) {
                matrix[i][j] = matrix[i - 1][j - 1];
            } else {
                matrix[i][j] = Math.min(
                    matrix[i - 1][j - 1] + 1, // substitution
                    Math.min(
                        matrix[i][j - 1] + 1, // insertion
                        matrix[i - 1][j] + 1  // deletion
                    )
                );
            }
        }
    }

    var distance = matrix[b.length][a.length];
    var maxLength = Math.max(a.length, b.length);
    return 1.0 - (distance / maxLength);
}
