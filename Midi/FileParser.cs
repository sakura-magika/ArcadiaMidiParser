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
using System.Linq;
using Stream = System.IO.Stream;
using BinaryReader = System.IO.BinaryReader;
using BitConverter = System.BitConverter;
using HeaderChunk = Midi.Chunks.HeaderChunk;
using TrackChunk = Midi.Chunks.TrackChunk;
using StringEncoder = System.Text.UTF7Encoding;
using TrackEventsIEnumerable = System.Collections.Generic.IEnumerable<Midi.Events.MidiEvent>;
using MidiEventList = System.Collections.Generic.List<Midi.Events.MidiEvent>;
using NoteOnEventList = System.Collections.Generic.List<Midi.Events.ChannelEvents.NoteOnEvent>;
using TimeSignatureEventList = System.Collections.Generic.List<Midi.Events.MetaEvents.TimeSignatureEvent>;
using ByteList = System.Collections.Generic.List<byte>;
using ByteEnumerable = System.Collections.Generic.IEnumerable<byte>;
using MidiEvent = Midi.Events.MidiEvent;
using MIDIEvent_Length_Tuple = System.Tuple<Midi.Util.Option.Option<Midi.Events.MidiEvent>, int, byte>;
using SomeMidiEvent = Midi.Util.Option.Some<Midi.Events.MidiEvent>;
using NoMidiEvent = Midi.Util.Option.None<Midi.Events.MidiEvent>;
using VariableLengthUtil = Midi.Util.VariableLengthUtil;
using Midi.Events.MetaEvents;
using SysexEvent = Midi.Events.SysexEvent;
using Midi.Events.ChannelEvents;
using System;

namespace Midi
{
    public class FileParser
    {
        private readonly static StringEncoder stringEncoder = new StringEncoder();

        public static MidiData Parse(Stream input_file_stream)
        {
            var input_binary_reader = new BinaryReader(input_file_stream);

            HeaderChunk header_chunk;
            ushort number_of_tracks;
            {
                var header_chunk_ID = stringEncoder.GetString(input_binary_reader.ReadBytes(4));
                var header_chunk_size = BitConverter.ToInt32(input_binary_reader.ReadBytes(4).Reverse().ToArray<byte>(), 0);
                var header_chunk_data = input_binary_reader.ReadBytes(header_chunk_size);

                var format_type = BitConverter.ToUInt16(header_chunk_data.Take(2).Reverse().ToArray<byte>(), 0);
                number_of_tracks = BitConverter.ToUInt16(header_chunk_data.Skip(2).Take(2).Reverse().ToArray<byte>(), 0);
                var time_division = BitConverter.ToUInt16(header_chunk_data.Skip(4).Take(2).Reverse().ToArray<byte>(), 0);

                header_chunk = new HeaderChunk(format_type, time_division);
            }

            var tracks =
                Enumerable.Range(0, number_of_tracks)
                .Select(track_number =>
                {
                    var track_chunk_ID = stringEncoder.GetString(input_binary_reader.ReadBytes(4));
                    var track_chunk_size = BitConverter.ToInt32(input_binary_reader.ReadBytes(4).Reverse().ToArray<byte>(), 0);
                    var track_chunk_data = input_binary_reader.ReadBytes(track_chunk_size);

                    return Tuple.Create(track_chunk_size, track_chunk_data);
                }).ToList()
                .Select(raw_track => parse_events(raw_track.Item2, raw_track.Item1));

            return new MidiData(header_chunk, tracks);
        }

        private static TrackChunk parse_events(ByteEnumerable track_data, int chunk_size)
        {
            var i = 0;
            var absolute_time = 0;
            var max_note_number = 0;
            var min_note_number = 127;
            var last_midi_channel = (byte)0x00;
            var midi_events = new MidiEventList();
            var note_on_events = new NoteOnEventList();
            var time_signature_events = new TimeSignatureEventList();
            var track_name_event = new SequenceOrTrackNameEvent("no name");
            var track_end_event_time = 0;

            while (i < chunk_size)
            {
                var tuple = next_event(track_data, i, last_midi_channel, ref absolute_time, note_on_events);
                i += tuple.Item2;
                last_midi_channel = tuple.Item3;

                if (tuple.Item1.GetType() == typeof(SomeMidiEvent))
                {
                    var midi_event = (tuple.Item1 as SomeMidiEvent).value;
                    midi_events.Add(midi_event);

                    if (midi_event.event_type == Events.MidiEventType.NoteOn)
                    {
                        var note_on = (NoteOnEvent) midi_event;
                        note_on_events.Add(note_on);

                        if (note_on.note_number > max_note_number)
                        {
                            max_note_number = note_on.note_number;
                        }

                        if (note_on.note_number < min_note_number)
                        {
                            min_note_number = note_on.note_number;
                        }
                    }
                    else if (midi_event.event_type == Events.MidiEventType.Meta)
                    {
                        var meta_event = (MetaEvent)midi_event;

                        if (meta_event.meta_event_type == Events.MidiEventType.TimeSignature)
                        {
                            time_signature_events.Add((TimeSignatureEvent)meta_event);
                        }
                        else if (meta_event.meta_event_type == Events.MidiEventType.EndOfTrack)
                        {
                            track_end_event_time = ((EndOfTrackEvent)meta_event).absolute_time;
                        }
                        else if (meta_event.meta_event_type == Events.MidiEventType.TrackName)
                        {
                            track_name_event = (SequenceOrTrackNameEvent)midi_event;
                        }
                    }
                }
            }

            var duration = absolute_time;

            // TODO: this seems to be cutting the file off at the last note's end, instead of the actual end, which is a problem:
            // if playing multiple files simultaneously, their ends may not be synchronized.
            // if I re-import the midi back into ableton, it is the correct length, not cut off, so there must be a way to fix this.
            if (track_end_event_time != 0)
            {
                duration = track_end_event_time;
            }

            return new TrackChunk(midi_events, note_on_events, time_signature_events, track_name_event.name, min_note_number, max_note_number, duration);
        }

        private static MIDIEvent_Length_Tuple next_event(ByteEnumerable track_data, int start_index, byte last_midi_channel, ref int absolute_time, NoteOnEventList note_on_list)
        {
            var i = start_index - 1;

            MidiEvent midi_event = null;
            {
                var delta_time = 0;
                {
                    var length_temp = new ByteList();
                    do
                    {
                        i += 1;
                        length_temp.Add(track_data.ElementAt(i));
                    } while (track_data.ElementAt(i) > 0x7F);

                    delta_time = VariableLengthUtil.decode_to_int(length_temp);
                }

                absolute_time += delta_time;
                i += 1;

                var event_type_value = track_data.ElementAt(i);

                // MIDI Channel Events
                if ((event_type_value & 0xF0) < 0xF0)
                {

                    var midi_channel_event_type = (byte)(event_type_value & 0xF0);
                    var midi_channel = (byte)(event_type_value & 0x0F);
                    i += 1;
                    var parameter_1 = track_data.ElementAt(i);
                    var parameter_2 = (byte)0x00;

                    // One or two parameter type
                    switch (midi_channel_event_type)
                    {
                        // One parameter types
                        case 0xC0:
                            midi_event = new ProgramChangeEvent(absolute_time, delta_time, midi_channel, parameter_1);
                            last_midi_channel = midi_channel;
                            break;
                        case 0xD0:
                            midi_event = new ChannelAftertouchEvent(absolute_time, delta_time, midi_channel, parameter_1);
                            last_midi_channel = midi_channel;
                            break;

                        // Two parameter types
                        case 0x80:
                            i += 1;
                            parameter_2 = track_data.ElementAt(i);
                            midi_event = new NoteOffEvent(absolute_time, delta_time, midi_channel, parameter_1, parameter_2);
                            last_midi_channel = midi_channel;
                            pair_note_off_event((NoteOffEvent) midi_event, note_on_list);
                            break;
                        case 0x90:
                            i += 1;
                            parameter_2 = track_data.ElementAt(i);
                            midi_event = new NoteOnEvent(absolute_time, delta_time, midi_channel, parameter_1, parameter_2);
                            last_midi_channel = midi_channel;
                            validate_note_on_event((NoteOnEvent) midi_event, note_on_list);
                            break;
                        case 0xA0:
                            i += 1;
                            parameter_2 = track_data.ElementAt(i);
                            midi_event = new NoteAftertouchEvent(absolute_time, delta_time, midi_channel, parameter_1, parameter_2);
                            last_midi_channel = midi_channel;
                            break;
                        case 0xB0:
                            i += 1;
                            parameter_2 = track_data.ElementAt(i);
                            midi_event = new ControllerEvent(absolute_time, delta_time, midi_channel, parameter_1, parameter_2);
                            last_midi_channel = midi_channel;
                            break;
                        case 0xE0:
                            i += 1;
                            parameter_2 = track_data.ElementAt(i);
                            midi_event = new PitchBendEvent(absolute_time, delta_time, midi_channel, parameter_1, parameter_2);
                            last_midi_channel = midi_channel;
                            break;
                        // Might be a Control Change Messages LSB
                        default:
                            midi_event = new ControllerEvent(absolute_time, delta_time, last_midi_channel, event_type_value, parameter_1);
                            break;
                    }

                    i += 1;
                }
                // Meta Events
                else if (event_type_value == 0xFF)
                {
                    i += 1;
                    var meta_event_type = track_data.ElementAt(i);
                    i += 1;
                    var meta_event_length = track_data.ElementAt(i);
                    i += 1;
                    var meta_event_data = Enumerable.Range(i, meta_event_length).Select(b => track_data.ElementAt(b)).ToArray();

                    switch (meta_event_type)
                    {
                        case 0x00:
                            midi_event = new SequenceNumberEvent(BitConverter.ToUInt16(meta_event_data.Reverse().ToArray<byte>(), 0));
                            break;
                        case 0x01:
                            midi_event = new TextEvent(absolute_time, delta_time, stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x02:
                            midi_event = new CopyrightNoticeEvent(stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x03:
                            midi_event = new SequenceOrTrackNameEvent(stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x04:
                            midi_event = new InstrumentNameEvent(absolute_time, delta_time, stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x05:
                            midi_event = new LyricsEvent(absolute_time, delta_time, stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x06:
                            midi_event = new MarkerEvent(absolute_time, delta_time, stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x07:
                            midi_event = new CuePointEvent(absolute_time, delta_time, stringEncoder.GetString(meta_event_data));
                            break;
                        case 0x20:
                            midi_event = new MIDIChannelPrefixEvent(absolute_time, delta_time, meta_event_data[0]);
                            break;
                        case 0x2F:
                            midi_event = new EndOfTrackEvent(absolute_time, delta_time);
                            break;
                        case 0x51:
                            var tempo =
                                (meta_event_data[2] & 0x0F) +
                                ((meta_event_data[2] & 0xF0) * 16) +
                                ((meta_event_data[1] & 0x0F) * 256) +
                                ((meta_event_data[1] & 0xF0) * 4096) +
                                ((meta_event_data[0] & 0x0F) * 65536) +
                                ((meta_event_data[0] & 0xF0) * 1048576);
                            midi_event = new SetTempoEvent(absolute_time, delta_time, tempo);
                            break;
                        case 0x54:
                            midi_event = new SMPTEOffsetEvent(absolute_time, delta_time, meta_event_data[0], meta_event_data[1], meta_event_data[2], meta_event_data[3], meta_event_data[4]);
                            break;
                        case 0x58:
                            midi_event = new TimeSignatureEvent(absolute_time, delta_time, meta_event_data[0], meta_event_data[1], meta_event_data[2], meta_event_data[3]);
                            break;
                        case 0x59:
                            midi_event = new KeySignatureEvent(absolute_time, delta_time, meta_event_data[0], meta_event_data[1]);
                            break;
                        case 0x7F:
                            midi_event = new SequencerSpecificEvent(absolute_time, delta_time, meta_event_data);
                            break;
                    }

                    i += meta_event_length;
                }
                // System Exclusive Events
                else if (event_type_value == 0xF0 || event_type_value == 0xF7)
                {

                    var event_length = 0;
                    {
                        var length_temp = new ByteList();
                        do
                        {
                            i += 1;
                            length_temp.Add(track_data.ElementAt(i));
                        } while (track_data.ElementAt(i) > 0x7F);

                        event_length = VariableLengthUtil.decode_to_int(length_temp);
                    }

                    i += 1;

                    var event_data = Enumerable.Range(i, event_length).Select(b => track_data.ElementAt(b));

                    midi_event = new SysexEvent(absolute_time, delta_time, event_type_value, event_data);

                    i += event_length;
                }
            }

            switch (midi_event != null)
            {
                case true:
                    return new MIDIEvent_Length_Tuple(new SomeMidiEvent(midi_event), i - start_index, last_midi_channel);
            }

            return new MIDIEvent_Length_Tuple(new NoMidiEvent(), i - start_index, last_midi_channel);
        }

        private static void pair_note_off_event(NoteOffEvent off_event, NoteOnEventList on_events)
        {
            var found_match = false;

            for (int i = (on_events.Count - 1); i >= 0; i--)
            {
                var on_event = on_events[i];

                if (on_event.note_number == off_event.note_number)
                {
                    if (on_event.note_off_event != null)
                    {
                        UnityEngine.Debug.LogErrorFormat("Found already-paired NoteOn event: {0} for NoteOff: {1}", on_event.ToString(), off_event.ToString());
                    }

                    var length = off_event.absolute_time - on_event.absolute_time;
                    on_event.update_length(length);
                    on_event.pair_note_off(off_event);
                    found_match = true;

                    break;
                }
            }

            if (found_match == false)
            {
                UnityEngine.Debug.LogErrorFormat("Failed to find NoteOn event for NoteOff: {0}", off_event.ToString());
            }
        }

        private static void validate_note_on_event(NoteOnEvent on_event, NoteOnEventList on_events)
        {
            for (int i = (on_events.Count - 1); i >= 0; i--)
            {
                var previous_on_event = on_events[i];

                if (on_event.note_number == previous_on_event.note_number)
                {
                    if (previous_on_event.note_off_event == null)
                    {
                        UnityEngine.Debug.LogErrorFormat("Found new NoteOn event: {0} for when previous NoteOn is unpaired: {1}", on_event.ToString(), previous_on_event.ToString());
                    }

                    break;
                }
            }
        }
    }
}
