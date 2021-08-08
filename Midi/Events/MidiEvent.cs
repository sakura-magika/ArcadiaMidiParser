/*
Copyright (c) 2013 Christoph Fabritz

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

namespace Midi.Events
{
	public enum MidiEventType
	{

		// control events
		NoteOff = 0x80,
		NoteOn = 0x90,
		KeyAfter = 0xA0,
		ControlChange = 0xB0,
		ProgramChange = 0xC0,
		ChannelAfter = 0xD0,
		PitchChange = 0xE0,
		Meta = 0xFF,
		System = 0xF0,

		// midi events
		SetTrackSequence = 0x00,
		TextEvent = 0x01,
		Copyright = 0x02,
		TrackName = 0x03,
		InstrumentName = 0x04,
		Lyric = 0x05,
		Marker = 0x06,
		Cue = 0x07,
		EndOfTrack = 0x2f,
		SetTempo = 0x51,
		TimeSignature = 0x58,
		KeySignature = 0x59,
		SequencerSpecific = 0x7F,
		TimingClock = 0xF8,
		StartSequence = 0xFA,
		ContinueSequence = 0xFB,
		StopSequence = 0xFC,

		Unknown = 0xDD
	};

	public abstract class MidiEvent
    {
		public readonly int absolute_time;
        public readonly int delta_time;
        public readonly MidiEventType event_type;

        public MidiEvent(int absolute_time, int delta_time, byte event_type)
        {
			this.absolute_time = absolute_time;
            this.delta_time = delta_time;
            this.event_type = (MidiEventType) event_type;
        }

        public override string ToString()
        {
            return "MidiEvent(absolute_time: " + absolute_time + " delta_time: " + delta_time + ", event_type: " + event_type + ")";
        }
    }
}
