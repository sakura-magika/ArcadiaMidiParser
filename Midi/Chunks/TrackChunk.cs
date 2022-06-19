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
using MidiEventList = System.Collections.Generic.List<Arcadia.Midi.Events.MidiEvent>;
using NoteOnEventList = System.Collections.Generic.List<Arcadia.Midi.Events.ChannelEvents.NoteOnEvent>;
using TimeSignatureEventList = System.Collections.Generic.List<Arcadia.Midi.Events.MetaEvents.TimeSignatureEvent>;
using MidiEvents = System.Collections.ObjectModel.ReadOnlyCollection<Arcadia.Midi.Events.MidiEvent>;
using NoteOnEvents = System.Collections.ObjectModel.ReadOnlyCollection<Arcadia.Midi.Events.ChannelEvents.NoteOnEvent>;
using TimeSignatureEvents = System.Collections.ObjectModel.ReadOnlyCollection<Arcadia.Midi.Events.MetaEvents.TimeSignatureEvent>;
using System.Linq;
using MidiEvent = Arcadia.Midi.Events.MidiEvent;

namespace Arcadia.Midi.Chunks
{
    public sealed class TrackChunk : Chunk
    {
        public readonly string name;
        public readonly MidiEvents midi_events;
        public readonly NoteOnEvents note_on_events;
        public readonly TimeSignatureEvents time_signatures;
        public readonly int min_note_number;
        public readonly int max_note_number;
        public readonly int duration;

        public TrackChunk(MidiEventList midi_events, NoteOnEventList note_on_events, TimeSignatureEventList time_signatures,
            string track_name, int min_note_number, int max_note_number, int duration)
            : base("MTrk")
        {
            this.name = track_name;
            this.midi_events = midi_events.AsReadOnly();
            this.note_on_events = note_on_events.AsReadOnly();
            this.time_signatures = time_signatures.AsReadOnly();
            this.min_note_number = min_note_number;
            this.max_note_number = max_note_number;
            this.duration = duration;
        }

        public (float beats_per_measure, float beat_unit_length) main_time_signature()
        {
            if (time_signatures.Count > 0)
            {
                var denominator = (float) time_signatures[0].denominator;
                var beat_unit_length = (float) System.Math.Pow(2.0f, denominator);
                var beats_per_measure = time_signatures[0].numerator;
                return (beats_per_measure, beat_unit_length);
            }
            else
            {
                return (4, 4);
            }
        }

        override public string ToString()
        {
            var events_string = midi_events.Aggregate("", (string a, MidiEvent b) => a + b + ", ");
            events_string = events_string.Remove(events_string.Length - 2);

            return string.Format("TrackChunk({0}, min_note: {1}, max_note: {2}, time_signature: ({3}) duration: {4}, events: [{5}])",
                base.ToString(), min_note_number, max_note_number, main_time_signature(), duration, events_string);
        }
    }
}
