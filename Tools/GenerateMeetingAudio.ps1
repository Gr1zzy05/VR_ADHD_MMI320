# Offline, editable placeholder narration. The WAV files are already included in the project.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$taskProject = Split-Path -Parent $PSScriptRoot
$taskAudioPath = Join-Path $taskProject 'Assets\HomeTasks\Audio'
New-Item -ItemType Directory -Force -Path $taskAudioPath | Out-Null
$taskSegments = @(
    'Hi, welcome to our neighbourhood check-in. I am Maya, your host. Thanks for joining from home. Keep listening as you work. At the end, I will ask you three short questions about our community garden event.'
    'First, the schedule. Our community garden event is on Saturday. Please arrive at ten thirty in the morning. That is Saturday at ten thirty A M. We will plant herbs together and then share a short tea break.'
    'Next, what to bring. Please bring the blue folder with your volunteer notes. You only need to remember the blue folder. Meet the group at the front gate of the community garden. The front gate is our meeting point.'
    'Before we finish, here is the plan once more. Saturday at ten thirty in the morning. Bring the blue folder, and meet us at the front gate. Thanks for listening. The briefing is over, and you can now answer the three questions on the screen.'
)
$taskNarrator = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $taskNarrator.SelectVoice('Microsoft Zira Desktop')
    $taskNarrator.Rate = -1
    for ($taskIndex = 0; $taskIndex -lt $taskSegments.Count; $taskIndex++) {
        $taskWav = Join-Path $taskAudioPath ('Meeting_{0:00}.wav' -f ($taskIndex + 1))
        $taskNarrator.SetOutputToWaveFile($taskWav)
        $taskNarrator.Speak($taskSegments[$taskIndex])
        $taskNarrator.SetOutputToNull()
        Write-Output $taskWav
    }
}
finally { $taskNarrator.Dispose() }
