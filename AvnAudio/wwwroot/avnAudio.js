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

    // Passed to getUserMedia
    const constraints = {
        audio: {
            deviceId: deviceId,
            channelCount: channels
        },
    };

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

        // try using getUserMedia, which doesn't always work
        navigator.mediaDevices.getUserMedia({ audio: true, video: false })
            .then(function (stream) {
                navigator.mediaDevices.enumerateDevices({ audio: true, video: false })
                    .then(function (devices) {
                        if (devices == null || devices.length == 0) {
                            dotNetObject.invokeMethodAsync("StatusChanged", "no devices found");
                            return;
                        }
                        // Call the .NET reference passing the array of devices
                        dotNetObject.invokeMethodAsync("AvailableAudioDevices", devices);
                        return;
                    })
                    .catch(function (err) {
                        dotNetObject.invokeMethodAsync("StatusChanged", err.name + ": " + err.message);
                        return;
                    });
            })
            .catch(function (err) {
                dotNetObject.invokeMethodAsync("StatusChanged", err.name + ": " + err.message);
                return;
            });

        // also try going straight to enumerateDevices
        navigator.mediaDevices.enumerateDevices({ audio: true, video: false })
            .then(function (devices) {
                if (devices == null || devices.length == 0) {
                    dotNetObject.invokeMethodAsync("StatusChanged", "no devices found");
                    return;
                }
                // Call the .NET reference passing the array of devices
                dotNetObject.invokeMethodAsync("AvailableAudioDevices", devices);
            })
            .catch(function (err) {
                dotNetObject.invokeMethodAsync("StatusChanged", err.name + ": " + err.message);
            });
    }
}

