// document-upload.js
var requestedDocType = "Aadhaar";

function startDocumentUpload(docType) {
    console.log("Document upload requested. Type: " + docType);
    requestedDocType = docType;
    $('#lblRequestedDocType').text(docType);
    $('#docUploadProgress').addClass('d-none');
    
    // Reset file inputs and preview areas
    $('#docUploadFront').val('');
    $('#docUploadBack').val('');
    
    $('#pnlFrontPreview').addClass('d-none');
    $('#pnlBackPreview').addClass('d-none');
    
    $('#imgFrontPreview').attr('src', '#');
    $('#imgBackPreview').attr('src', '#');
}

$(document).ready(function () {
    // Helper function to render a local preview instantly
    function previewLocalFile(file, side) {
        var previewImgId = side === 'front' ? '#imgFrontPreview' : '#imgBackPreview';
        var previewPnlId = side === 'front' ? '#pnlFrontPreview' : '#pnlBackPreview';

        var reader = new FileReader();
        reader.onload = function (event) {
            $(previewImgId).attr('src', event.target.result);
            $(previewPnlId).removeClass('d-none');
        };
        reader.readAsDataURL(file);
    }

    // Helper function to handle upload
    function uploadImageSide(file, side) {
        if (!file) return;

        // Instantly preview locally
        previewLocalFile(file, side);

        // Disable file inputs to prevent parallel upload race conditions
        $('#docUploadFront').prop('disabled', true);
        $('#docUploadBack').prop('disabled', true);

        // Display progress indicator
        $('#docUploadProgress').removeClass('d-none');
        $('#lblUploadProgressText').text("Uploading and analyzing " + side + " side...");

        var fd = new FormData();
        fd.append('document', file);
        fd.append('sessionId', sessionId);
        fd.append('docType', requestedDocType);
        fd.append('side', side);

        console.log("Uploading " + side + " side image file for OCR analysis...");

        fetch('/Handlers/UploadDocument.ashx', {
            method: 'POST',
            body: fd
        })
        .then(response => response.json())
        .then(data => {
            console.log("OCR document verification result (" + side + "): ", data);
            
            // Hide progress indicator
            $('#docUploadProgress').addClass('d-none');

            // Enable file inputs
            $('#docUploadFront').prop('disabled', false);
            $('#docUploadBack').prop('disabled', false);

            // Send OCR extraction results back to officer
            kycProxy.invoke('sendVerificationResult', sessionId, 'document', JSON.stringify(data));
        })
        .catch(err => {
            console.error("Document upload failed: ", err);
            $('#docUploadProgress').addClass('d-none');
            
            // Enable file inputs
            $('#docUploadFront').prop('disabled', false);
            $('#docUploadBack').prop('disabled', false);

            alert(side.charAt(0).toUpperCase() + side.slice(1) + " side document analysis failed. Please check the image and try again.");
        });
    }

    // Bind front file selection
    $('#docUploadFront').on('change', function (e) {
        var file = e.target.files[0];
        if (file) {
            uploadImageSide(file, 'front');
        }
    });

    // Bind back file selection
    $('#docUploadBack').on('change', function (e) {
        var file = e.target.files[0];
        if (file) {
            uploadImageSide(file, 'back');
        }
    });
});
