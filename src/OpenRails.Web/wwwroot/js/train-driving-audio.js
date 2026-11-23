let context, engineOscillator, engineGain, brakeNoiseSource, brakeFilter, brakeGain;
let enabled = false;

const clamp = (value, min, max) => Math.max(min, Math.min(max, Number(value) || 0));
export function calculateSoundParameters(throttlePercent, speedKmh, brakePercent) {
    const throttle = clamp(throttlePercent, 0, 100) / 100;
    const speed = clamp(speedKmh, 0, 180);
    const brake = clamp(brakePercent, 0, 100) / 100;
    return {
        engineFrequencyHz: 38 + throttle * 95 + speed * 0.35,
        engineGain: 0.006 + throttle * 0.022 + (speed / 180) * 0.008,
        brakeGain: brake * 0.009
    };
}

export async function start() {
    if (context) {
        await context.resume();
        setEnabled(true);
        return;
    }
    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextType) throw new Error("Web Audio is unavailable in this browser.");
    context = new AudioContextType();
    engineOscillator = context.createOscillator();
    engineOscillator.type = "sawtooth";
    engineGain = context.createGain();
    engineGain.gain.value = 0;
    engineOscillator.connect(engineGain);
    engineGain.connect(context.destination);
    engineOscillator.start();

    const noiseBuffer = context.createBuffer(1, context.sampleRate, context.sampleRate);
    const samples = noiseBuffer.getChannelData(0);
    for (let i = 0; i < samples.length; i++) samples[i] = (Math.random() * 2 - 1) * 0.15;
    brakeNoiseSource = context.createBufferSource();
    brakeNoiseSource.buffer = noiseBuffer;
    brakeNoiseSource.loop = true;
    brakeFilter = context.createBiquadFilter();
    brakeFilter.type = "bandpass";
    brakeFilter.frequency.value = 750;
    brakeFilter.Q.value = 0.7;
    brakeGain = context.createGain();
    brakeGain.gain.value = 0;
    brakeNoiseSource.connect(brakeFilter);
    brakeFilter.connect(brakeGain);
    brakeGain.connect(context.destination);
    brakeNoiseSource.start();
    await context.resume();
    setEnabled(true);
}

export function setEnabled(value) {
    enabled = Boolean(value) && Boolean(context);
    if (engineGain) engineGain.gain.setTargetAtTime(enabled ? 0.006 : 0, context.currentTime, 0.05);
    if (brakeGain) brakeGain.gain.setTargetAtTime(0, context.currentTime, 0.05);
}

export function update(throttlePercent, speedKmh, brakePercent) {
    if (!enabled || !context) return;
    const targets = calculateSoundParameters(throttlePercent, speedKmh, brakePercent);
    engineOscillator.frequency.setTargetAtTime(targets.engineFrequencyHz, context.currentTime, 0.08);
    engineGain.gain.setTargetAtTime(targets.engineGain, context.currentTime, 0.08);
    brakeGain.gain.setTargetAtTime(targets.brakeGain, context.currentTime, 0.08);
}

export function dispose() {
    enabled = false;
    if (engineOscillator) { try { engineOscillator.stop(); } catch { } }
    if (brakeNoiseSource) { try { brakeNoiseSource.stop(); } catch { } }
    engineOscillator?.disconnect(); engineGain?.disconnect(); brakeNoiseSource?.disconnect();
    brakeFilter?.disconnect(); brakeGain?.disconnect();
    if (context) { try { context.close(); } catch { } }
    context = engineOscillator = engineGain = brakeNoiseSource = brakeFilter = brakeGain = null;
}
