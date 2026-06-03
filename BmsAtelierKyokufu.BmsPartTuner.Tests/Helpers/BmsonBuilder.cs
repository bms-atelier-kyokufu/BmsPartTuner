using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// <see cref="BmsonFormat"/> オブジェクトを流れるようなインターフェースで構築するためのビルダー。
    /// <see cref="IBmsFamilyBuilder"/> インターフェースを実装し、<see cref="BmsBuilder"/> とのインターフェースの共通化を図ります。
    /// </summary>
    public class BmsonBuilder(BmsFamilyTestContext context) : IBmsFamilyBuilder<BmsonBuilder>
    {
        public static BmsonBuilder Create(BmsFamilyTestContext context) => new(context);

        private readonly BmsFamilyTestContext _context = context;
        private int _resolution = 240;
        private double _initBpm = 130.0;
        private string _title = "";
        private string _genre = "";
        private string _artist = "";
        private int _level = 1;
        private double _judgeRank = 100.0;
        private double _total = 100.0;

        private readonly List<BmsonBpmEvent> _bpmEvents = [];
        private readonly List<BmsonStopEvent> _stopEvents = [];
        private readonly List<BmsonLineEvent> _lines = [];
        private readonly List<BmsonSoundChannel> _soundChannels = [];

        private readonly Dictionary<string, string> _wavIndexMap = [];
        private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public BmsonBuilder WithInfo(
            int? resolution = null,
            double? initBpm = null,
            string? title = null,
            string? genre = null,
            string? artist = null,
            int? level = null,
            double? judgeRank = null,
            double? total = null)
        {
            if (resolution.HasValue) _resolution = resolution.Value;
            if (initBpm.HasValue) _initBpm = initBpm.Value;
            if (title != null) _title = title;
            if (genre != null) _genre = genre;
            if (artist != null) _artist = artist;
            if (level.HasValue) _level = level.Value;
            if (judgeRank.HasValue) _judgeRank = judgeRank.Value;
            if (total.HasValue) _total = total.Value;
            return this;
        }

        public enum BmsHeaderKey
        {
            Title,
            Genre,
            Artist,
            Bpm,
            PlayLevel,
            Rank,
            Total,
            Resolution
        }

        public BmsonBuilder WithHeader(BmsHeaderKey key, string value)
        {
            switch (key)
            {
                case BmsHeaderKey.Title:
                    _title = value;
                    break;
                case BmsHeaderKey.Genre:
                    _genre = value;
                    break;
                case BmsHeaderKey.Artist:
                    _artist = value;
                    break;
                case BmsHeaderKey.Bpm:
                    _initBpm = double.Parse(value);
                    break;
                case BmsHeaderKey.PlayLevel:
                    _level = int.Parse(value);
                    break;
                case BmsHeaderKey.Rank:
                    int rankVal = int.Parse(value);
                    _judgeRank = rankVal switch
                    {
                        0 => 30,
                        1 => 50,
                        2 => 80,
                        _ => 100
                    };
                    break;
                case BmsHeaderKey.Total:
                    _total = double.Parse(value);
                    break;
                case BmsHeaderKey.Resolution:
                    _resolution = int.Parse(value);
                    break;
            }
            return this;
        }

        public BmsonBuilder WithWav(int index, string filename, bool createFile = true, bool writeToDisk = true)
        {
            string indexStr = ToBmsIndex(index);
            return WithWav(indexStr, filename, createFile, writeToDisk);
        }

        public BmsonBuilder WithWav(string indexStr, string filename, bool createFile = true, bool writeToDisk = true)
        {
            _wavIndexMap[indexStr] = filename;
            if (createFile && writeToDisk)
            {
                var path = Path.Combine(_context.TempDirectory, filename);
                BmsTestWavHelper.CreateSilenceWavFile(path, 0.1, 2); // Use CreateSilenceWavFile or CreateDummyWavFile as appropriate
            }
            return this;
        }

        public BmsonBuilder AddMainData(int measure, int channel, string data)
        {
            if (string.IsNullOrEmpty(data)) return this;

            int length = data.Length / 2;
            long measureLengthInPulses = _resolution * 4;
            long measureStartPulse = measure * measureLengthInPulses;

            for (int i = 0; i < length; i++)
            {
                string wavIndex = data.Substring(i * 2, 2);
                if (wavIndex == "00") continue;

                if (_wavIndexMap.TryGetValue(wavIndex, out string? filename) && filename != null)
                {
                    long y = measureStartPulse + (i * measureLengthInPulses / length);
                    int x = MapChannelToLane(channel);
                    AddNoteToChannel(filename, new BmsonNote { X = x, Y = y, C = false });
                }
            }

            long nextMeasureStart = (measure + 1) * measureLengthInPulses;
            for (long m = 1; m <= measure + 1; m++)
            {
                long lineY = m * measureLengthInPulses;
                if (!_lines.Any(l => l.Y == lineY))
                {
                    _lines.Add(new BmsonLineEvent { Y = lineY });
                }
            }

            return this;
        }

        public BmsonBuilder AddMainData(int channel, string data)
        {
            return AddMainData(1, channel, data);
        }

        public BmsonBuilder AddBpmEvent(long y, double bpm)
        {
            _bpmEvents.Add(new BmsonBpmEvent { Y = y, Bpm = bpm });
            return this;
        }

        public BmsonBuilder AddBpmEvents(IEnumerable<BmsonBpmEvent> bpmEvents)
        {
            _bpmEvents.AddRange(bpmEvents);
            return this;
        }

        public BmsonBuilder AddStopEvent(long y, long duration)
        {
            _stopEvents.Add(new BmsonStopEvent { Y = y, Duration = duration });
            return this;
        }

        public BmsonBuilder AddStopEvents(IEnumerable<BmsonStopEvent> stopEvents)
        {
            _stopEvents.AddRange(stopEvents);
            return this;
        }

        public BmsonBuilder AddLine(long y)
        {
            _lines.Add(new BmsonLineEvent { Y = y });
            return this;
        }

        public BmsonBuilder AddLines(IEnumerable<BmsonLineEvent> lines)
        {
            _lines.AddRange(lines);
            return this;
        }

        public BmsonBuilder AddSoundChannel(string name, params BmsonNote[] notes)
        {
            _soundChannels.Add(new BmsonSoundChannel
            {
                Name = name,
                Notes = [.. notes]
            });
            return this;
        }

        public BmsonBuilder AddSoundChannels(IEnumerable<BmsonSoundChannel> soundChannels)
        {
            _soundChannels.AddRange(soundChannels);
            return this;
        }

        public BmsonFormat Build()
        {
            return new BmsonFormat
            {
                Info = new BmsonInfo
                {
                    Resolution = _resolution,
                    InitBpm = _initBpm,
                    Title = _title,
                    Genre = _genre,
                    Artist = _artist,
                    Level = _level,
                    JudgeRank = _judgeRank,
                    Total = _total
                },
                BpmEvents = [.. _bpmEvents],
                StopEvents = [.. _stopEvents],
                Lines = [.. _lines],
                SoundChannels = [.. _soundChannels]
            };
        }

        // Explicit implementations of IBmsBuilder return type mappings
        IBmsFamilyBuilder IBmsFamilyBuilder.WithHeader(string key, string value)
        {
            if (Enum.TryParse<BmsHeaderKey>(key, true, out var headerKey))
            {
                WithHeader(headerKey, value);
            }
            return this;
        }
        IBmsFamilyBuilder IBmsFamilyBuilder.WithWav(int index, string filename, bool createFile, bool writeToDisk) => WithWav(index, filename, createFile, writeToDisk);
        IBmsFamilyBuilder IBmsFamilyBuilder.WithWav(string indexStr, string filename, bool createFile, bool writeToDisk) => WithWav(indexStr, filename, createFile, writeToDisk);
        IBmsFamilyBuilder IBmsFamilyBuilder.AddMainData(int measure, int channel, string data) => AddMainData(measure, channel, data);
        IBmsFamilyBuilder IBmsFamilyBuilder.AddMainData(int channel, string data) => AddMainData(channel, data);

        private void AddNoteToChannel(string filename, BmsonNote note)
        {
            var channel = _soundChannels.FirstOrDefault(c => c.Name == filename);
            if (channel == null)
            {
                channel = new BmsonSoundChannel { Name = filename, Notes = [] };
                _soundChannels.Add(channel);
            }
            channel.Notes.Add(note);
        }

        private static int MapChannelToLane(int channel)
        {
            if (channel == 1) return 0; // BGM

            int prefix = channel / 10;
            int suffix = channel % 10;

            int isDouble = (prefix == 2 || prefix == 6) ? 8 : 0;
            int laneOffset = suffix switch
            {
                8 => 6,
                9 => 7,
                6 => 8,
                int n => n
            };

            return laneOffset + isDouble;
        }

        private static string ToBmsIndex(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            string result = "";
            int target = index;

            if (target == 0) return "00";

            while (target > 0)
            {
                result = Base36Chars[target % 36] + result;
                target /= 36;
            }

            if (result.Length < 2)
            {
                result = result.PadLeft(2, '0');
            }

            return result;
        }
    }
}
