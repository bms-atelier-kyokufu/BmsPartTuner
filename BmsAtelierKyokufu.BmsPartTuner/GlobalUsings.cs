global using System;
global using System.Buffers;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Input;
global using System.Windows.Media;
global using System.Windows.Threading;

global using BmsAtelierKyokufu.BmsPartTuner.Core;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Comparison;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Processing;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Processing.Pipeline;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Virtual;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Common;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Messages;
global using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
global using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;
global using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Common;
global using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Diagnostics;
global using BmsAtelierKyokufu.BmsPartTuner.Models;
global using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using CommunityToolkit.Mvvm.Messaging;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BmsAtelierKyokufu.BmsPartTuner.Tests")]
