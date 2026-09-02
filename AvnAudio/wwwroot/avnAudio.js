// MediaRecorder class required across methods.
var avnAudioRecorder;

// Stop Recording
export function stopRecording() {
    // Make sure we have a MediaRecorder object
    if (avnAudioRecorder == null) {
        return;
    }
    // stop and clean up
    avnAudioRecorder.stop();
    avnAudioRecorder.dataavailable = null;
    avnAudioRecorder = null;
}

// Start recording
export async function startRecording(dotNetObject, deviceId, sampleRate,
    channels, timeSlice) {

    // Passed to getUserMedia.
    // Use an "exact" constraint so the browser is FORCED to open the
    // selected device. A plain string is an "ideal" constraint and the
    // browser may silently fall back to another device (e.g. a virtual
    // cable), which is what produced the flat-line recordings.
    const audioConstraints = {
        channelCount: channels
    };
    if (deviceId !== null && deviceId !== undefined && deviceId !== "") {
        audioConstraints.deviceId = { exact: deviceId };
    }
    const constraints = { audio: audioConstraints };

    // passed to MediaRecorder constructor
    const options = {
        mimeType: "audio/webm",
        audioBitsPerSecond: sampleRate,
    };

    // boolean set when recording has been stopped,
    // but there is still data to process
    var stopped = false;

    // First we need to retrieve the device
    navigator.mediaDevices.getUserMedia(constraints)
        .then(function (stream) {
            let recorder = new MediaRecorder(stream, options);
            if (recorder == null) {
                console.log("recorder is null");
                return;
            }

            // now we have a recorder.

            // Handle the stop event
            recorder.addEventListener("stop", (e) => {
                // We've stopped recording,
                // but there is still data to process.
                // Set this flag
                stopped = true;
            });

            // Handle the dataavailable event
            recorder.addEventListener('dataavailable', function (e) {
                // we have a buffer!!
                try {
                    // convert it to a base 64 string
                    var reader = new window.FileReader();
                    reader.onloadend = function () {
                        var base64String = btoa(String.fromCharCode.apply(null, new Uint8Array(reader.result)));
                        // Send the buffer up to the AvnAudio component
                        dotNetObject.invokeMethodAsync("DataAvailable", base64String);
                        // If we've stopped, tell the component
                        if (stopped) {
                            dotNetObject.invokeMethodAsync("RecordingStoppedCallback");
                        }
                    }
                    reader.readAsArrayBuffer(e.data);
                }
                catch (err) {
                    console.log(err);
                }

            });

            // Set this global variable
            avnAudioRecorder = recorder;

            // Start the recorder with the timeslice MS value
            recorder.start(timeSlice);

            // Tell the component we've started
            dotNetObject.invokeMethodAsync("RecordingStartedCallback");
        })
        .catch(function (err) {
            // The "exact" constraint makes getUserMedia throw if the
            // selected device can't be opened (OverconstrainedError,
            // NotFoundError, NotAllowedError, ...). Surface it instead
            // of failing silently.
            console.error("[avnAudio] getUserMedia failed:", err.name, err.message);
            dotNetObject.invokeMethodAsync("StatusChanged",
                "Could not open the selected audio device: " + err.name + " - " + err.message);
        });
}

// enumerate audio devices
export function enumerateAudioDevices(dotNetObject) {

    // Ensure the browser supports AudioContext
    if (!window.AudioContext) {
        if (!window.webkitAudioContext) {
            dotNetObject.invokeMethodAsync("StatusChanged", "Your browser does not support AudioContext.");
            return;
        }
        window.AudioContext = window.webkitAudioContext;
    }

    // Query the media devices
    if (window.AudioContext) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
            // Make sure we CAN
            dotNetObject.invokeMethodAsync("StatusChanged", "enumerateDevices() not supported.");
            return;
        }

        // Request permission via getUserMedia first (this is what unlocks
        // real device labels in most browsers), then enumerate exactly once.
        // If getUserMedia is denied, fall back to a direct enumerate so we
        // still get the device ids (labels may be blank in that case).
        //
        // IMPORTANT: AvailableAudioDevices must be called exactly ONCE.
        // Calling it twice re-renders the <select> and resets the user's
        // selection to the first option, which silently discards their choice.
        navigator.mediaDevices.getUserMedia({ audio: true, video: false })
            .then(function (stream) {
                // We only needed the permission prompt; release the tracks.
                stream.getTracks().forEach(function (track) { track.stop(); });
                return navigator.mediaDevices.enumerateDevices();
            })
            .catch(function () {
                // Permission denied or getUserMedia unsupported: enumerate anyway.
                return navigator.mediaDevices.enumerateDevices();
            })
            .then(function (devices) {
                if (devices == null || devices.length == 0) {
                    dotNetObject.invokeMethodAsync("StatusChanged", "no devices found");
                    return;
                }
                // Call the .NET reference passing the array of devices (once).
                dotNetObject.invokeMethodAsync("AvailableAudioDevices", devices);
            })
            .catch(function (err) {
                dotNetObject.invokeMethodAsync("StatusChanged", err.name + ": " + err.message);
            });
    }
}

