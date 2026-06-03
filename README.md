
# OpenUtau

OpenUtau is a free, open-source editor made for the UTAU community. This fork is aimed at tailoring OpenUtau to REAPER and it's capabilities.


## Pre-requisits
The only thing you need is a 16ch virual audio device and, obviously, REAPER. For MacOS I recommend using BlackHole, for Windows, I believe, ReaRoute should do just fine, if not, use VB-Cable. 


## Getting Started
Go to your REAPER settings, into Control/OSC/Web tab. Create two OSC devices, both with Pattern Config "Default", one Device/IP Port (Send only), one Local port (Recieve only). For send port put 8001 in "Device Port", for recieve port 8000. Both should have the same Local IP adress. Launch OpenUtau. In Playback preferences, select your virtual audio device. That's pretty much it. Just create a Track in REAPER, select input 1 of your virtual audio device, arm it for recording, create a track in OpenUtau, and voilà, you should hear whatever happening on track 1 in your OU on your track 1 in REAPER. Need a second track? Just duplicate that track, and choose input 2 instead of input 1, and you'll hear whatever is happening on track 2 of your OU in REAPER. Happy mixing!


## Main features
- Full playback synchronization (Pseudo-ARA)
- Track 1-14 are now mono-output, so it is possible to track and mix directly in DAW (as long as you are using low-latency plugins, that is)
- OpenUtau -> REAPER BPM sync
- Works with MacOS/Windows/Linux


## Planned features
- Full MIDI syncronization
- Two-sided BPM syncronization
- Maybe I will come up with a pretty solution to PDC, but, that's unlikely


## Notes/Known issues
- It might be slightly desynced first couple times you start playback, due to how OpenUtau deals with render caching. Fixes itself after first three to five playbacks
- Recommended to stop playback manually
- AFAIK, latency on master track doesn't influence syncronization, unlike latency on track with OU itself. Treat it like you would live-instrument, not like typical VSTi synth.


## AI Usage Disclaimer
This project is 80% vibe-coded, as I, unfortunately, don't know much about coding at this scale. Sorry!
