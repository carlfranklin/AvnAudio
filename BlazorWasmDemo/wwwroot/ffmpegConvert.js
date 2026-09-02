// ffmpeg.wasm conversion helper for the Blazor WASM demo.
//
// Uses the UMD build loaded via <script src="ffmpeg.js"> in index.html,
// which exposes the global FFmpegWASM.FFmpeg. The web worker chunk
// (814.ffmpeg.js) is spawned by the UMD build from the same directory.
// The core files (ffmpeg-core.js / ffmpeg-core.wasm) are self-hosted in
// wwwroot and loaded via blob URLs (the documented approach) to avoid
// CORS and relative-URL resolution issues inside the worker.
//
// The FFmpeg instance is created and loaded once, then reused for every
// conversion.

let ffmpegInstance = null;
let ffmpegPromise = null;

// Fetch a same-origin file and return an object URL for it.
// This is the approach recommended by the ffmpeg.wasm docs for
// self-hosted core files.
async function toBlobURL(url, mimeType) {
    const res = await fetch(url);
    if (!res.ok) {
        throw new Error("Failed to fetch " + url + " (" + res.status + ")");
    }
    const buf = await res.arrayBuffer();
    return URL.createObjectURL(new Blob([buf], { type: mimeType }));
}

// Returns a loaded FFmpeg instance, creating and loading it on first use.
function getFFmpeg() {
    if (ffmpegInstance) {
        return Promise.resolve(ffmpegInstance);
    }
    if (ffmpegPromise) {
        return ffmpegPromise;
    }

    ffmpegPromise = (async () => {
        const ff = new FFmpegWASM.FFmpeg();

        // Surface ffmpeg's own log output in the browser console.
        ff.on("log", ({ message }) => {
            console.log("[ffmpeg.wasm]", message);
        });

        // Load the self-hosted core via blob URLs.
        const coreURL = await toBlobURL("ffmpeg-core.js", "text/javascript");
        const wasmURL = await toBlobURL("ffmpeg-core.wasm", "application/wasm");
        await ff.load({ coreURL, wasmURL });

        ffmpegInstance = ff;
        return ff;
    })().catch((err) => {
        // Reset so a later call can retry after a load failure.
        ffmpegPromise = null;
        throw err;
    });

    return ffmpegPromise;
}

// Convert a base64-encoded WebM (Opus) to a base64-encoded WAV (PCM).
export async function convertWebmToWav(base64Webm) {
    const ff = await getFFmpeg();

    const inputBytes = base64ToUint8Array(base64Webm);
    await ff.writeFile("input.webm", inputBytes);

    // exec() prepends ["-nostdin", "-y"] automatically.
    const code = await ff.exec(["-i", "input.webm", "output.wav"]);
    if (code !== 0) {
        throw new Error("ffmpeg conversion failed with exit code " + code);
    }

    const wavBytes = await ff.readFile("output.wav");

    // Clean up the virtual filesystem.
    await ff.deleteFile("input.webm");
    await ff.deleteFile("output.wav");

    return uint8ArrayToBase64(wavBytes);
}

// Decode a base64 string into a Uint8Array.
function base64ToUint8Array(base64) {
    const binaryString = atob(base64);
    const len = binaryString.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes;
}

// Encode a Uint8Array into a base64 string (chunked to avoid call-stack
// limits on large buffers).
function uint8ArrayToBase64(bytes) {
    let binaryString = "";
    const chunkSize = 0x8000;
    for (let i = 0; i < bytes.length; i += chunkSize) {
        binaryString += String.fromCharCode.apply(
            null,
            bytes.subarray(i, i + chunkSize)
        );
    }
    return btoa(binaryString);
}
