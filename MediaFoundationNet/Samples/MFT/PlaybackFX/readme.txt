/************************************************************************

PlaybackFX - An upgraded version of the BasicPlayer sample that uses transforms

**************************************************************************/

This sample is a c# version of the PlaybackFX sample included in the
Media Foundation SDK.  It is the same as the BasicPlayer sample, except 
that it loads one of several MFTs into the topology.

In addition to the MFTs in this project, you can also select the c++
MFT (if you have built and registered it).

Note that the TypeConverter has no visible effect on the output.  Still,
it is converting the type from YUY2 to RGB32.

A quirk: The menus are disabled while the session is playing, and don't
get re-enabled until the playback is complete.