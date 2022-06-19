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

namespace Arcadia.Midi.Events.ChannelEvents
{
    public sealed class NoteOnEvent : ChannelEvent
    {
        public byte note_number
        {
            get
            {
                return this.parameter_1;
            }
        }
        public byte velocity
        {
            get
            {
                return this.parameter_2;
            }
        }

        public int note_length { get; private set; }

        public NoteOffEvent note_off_event { get; private set; }

        public NoteOnEvent(int absolute_time, int delta_time, byte midi_channel, byte note_number, byte velocity)
            : base(absolute_time, delta_time, 0x90, midi_channel, note_number, velocity)
        {
        }

        public void update_length(int length)
        {
            note_length = length;
        }

        public void pair_note_off(NoteOffEvent note_off_event)
        {
            this.note_off_event = note_off_event;
        }

        public override string ToString()
        {
            return "NoteOnEvent(" + base.ToString() + ", note_number: " + note_number + ", velocity: " + velocity + ", note_length: " + note_length + ")";
        }
    }
}

