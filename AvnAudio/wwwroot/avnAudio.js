var avnAudioRecorder;

export function stopRecording() {
    if (avnAudioRecorder == null) {
        return;
    }
    avnAudioRecorder.stop();
    avnAudioRecorder.dataavailable = null;
    avnAudioRecorder = null;
}

export async function startRecording(dotNetObject, deviceId, sampleRate, channels, timeSlice) {
    const constraints = {
        audio: {
            deviceId: deviceId,
            channelCount: channels
        },
    };
    const options = {
        mimeType: "audio/webm",
        audioBitsPerSecond: sampleRate,
    };

    // boolean set when recording has been stopped,
    // but there is still data to process
    var stopped = false;

    navigator.mediaDevices.getUserMedia(constraints)
        .then(function (stream) {
            let recorder = new MediaRecorder(stream, options);
            if (recorder == null) {
                console.log("recorder is Null");
                return;
            }
            recorder.addEventListener("stop", (e) => {
                // We've stopped recording,
                // but there is still data to process.
                // Set this flag
                stopped = true;
            });
            recorder.addEventListener('dataavailable', function (e) {
                // we have a buffer!!
                try {
                    // convert it to a base 64 string
                    var reader = new window.FileReader();
                    reader.onloadend = function () {
                        var base64String = btoa(String.fromCharCode.apply(null, new Uint8Array(reader.result)));
                        // Send the buffer up to the AvnAudio component
                        //console.log("");
                        //console.log("BUFFER:");
                        //console.log(base64String);
                        //console.log("");
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
            avnAudioRecorder = recorder;
            recorder.start(timeSlice);
            dotNetObject.invokeMethodAsync("RecordingStartedCallback");
        });
}

// enumerate audio devices
export function enumerateAudioDevices(dotNetObject) {

    if (!window.AudioContext) {
        if (!window.webkitAudioContext) {
            dotNetObject.invokeMethodAsync("StatusChanged", "Your browser does not support AudioContext.");
            return;
        }
        window.AudioContext = window.webkitAudioContext;
    }
    if (window.AudioContext) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
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
                    })
                    .catch(function (err) {
                        dotNetObject.invokeMethodAsync("StatusChanged", err.name + ": " + err.message);

                    });
            })
            .catch(function (err) {
                dotNetObject.invokeMethodAsync("StatusChanged", err.name + ": " + err.message);
                enumerateJitsiDevices();
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

