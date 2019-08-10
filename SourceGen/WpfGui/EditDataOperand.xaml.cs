﻿/*
 * Copyright 2019 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

using CommonUtil;

namespace SourceGen.WpfGui {
    /// <summary>
    /// Data operand editor.
    /// </summary>
    public partial class EditDataOperand : Window, INotifyPropertyChanged {
        /// <summary>
        /// Result set that describes the formatting to perform.  Not all regions will have
        /// the same format, e.g. the "mixed ASCII" mode will alternate strings and bytes
        /// (rather than a dedicated "mixed ASCII" format type).
        /// </summary>
        public SortedList<int, FormatDescriptor> Results { get; private set; }

        /// <summary>
        /// Set to true when input is valid.  Controls whether the OK button is enabled.
        /// </summary>
        public bool IsValid {
            get { return mIsValid; }
            set {
                mIsValid = value;
                OnPropertyChanged();
            }
        }
        private bool mIsValid;

        /// <summary>
        /// Selected offsets.  An otherwise contiguous range of offsets can be broken up
        /// by user-specified labels and address discontinuities, so this needs to be
        /// processed by range.
        /// </summary>
        private TypedRangeSet mSelection;

        /// <summary>
        /// FormatDescriptor from the first offset.  May be null if the offset doesn't
        /// have a format descriptor specified.  This will be used to configure the
        /// dialog controls if the format is suited to the selection.  The goal is to
        /// make single-item editing work as expected.
        /// </summary>
        public FormatDescriptor mFirstFormatDescriptor;

        /// <summary>
        /// Raw file data.
        /// </summary>
        private byte[] mFileData;

        /// <summary>
        /// Symbol table to use when resolving symbolic values.
        /// </summary>
        private SymbolTable mSymbolTable;

        /// <summary>
        /// Formatter to use when displaying addresses and hex values.
        /// </summary>
        private Asm65.Formatter mFormatter;

        ///// <summary>
        ///// Set this during initial control configuration, so we know to ignore the CheckedChanged
        ///// events.
        ///// </summary>
        //private bool mIsInitialSetup;

        /// <summary>
        /// Set to true if, during the initial setup, the format defined by FirstFormatDescriptor
        /// was unavailable.
        /// </summary>
        private bool mPreferredFormatUnavailable;

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public EditDataOperand(Window owner, byte[] fileData, SymbolTable symbolTable,
                Asm65.Formatter formatter, TypedRangeSet trs, FormatDescriptor firstDesc) {
            InitializeComponent();
            Owner = owner;
            DataContext = this;

            mFileData = fileData;
            mSymbolTable = symbolTable;
            mFormatter = formatter;
            mSelection = trs;
            mFirstFormatDescriptor = firstDesc;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            DateTime startWhen = DateTime.Now;

            // Determine which of the various options is suitable for the selected offsets.
            // Disable any radio buttons that won't work.
            AnalyzeRanges();

            // Configure the dialog from the FormatDescriptor, if one is available.
            Debug.WriteLine("First FD: " + mFirstFormatDescriptor);
            SetControlsFromDescriptor(mFirstFormatDescriptor);

            if (mPreferredFormatUnavailable) {
                // This can happen when e.g. a bunch of stuff is formatted as null-terminated
                // strings.  We don't recognize a lone zero as a string, but we allow it if
                // it's next to a bunch of others.  If you come back later and try to format
                // just that one byte, you end up here.
                // TODO(maybe): make it more obvious what's going on?
                Debug.WriteLine("NOTE: preferred format unavailable");
            }

            UpdateControls();

            Debug.WriteLine("EditData dialog load time: " +
                (DateTime.Now - startWhen).TotalMilliseconds + " ms");
        }

        private void Window_ContentRendered(object sender, EventArgs e) {
            // Start with the focus in the text box if the initial format allows for a
            // symbolic reference.  This way they can start typing immediately.
            if (simpleDisplayAsGroupBox.IsEnabled) {
                symbolEntryTextBox.Focus();
            }
        }

        /// <summary>
        /// Handles Checked event for all buttons in Main group.
        /// </summary>
        private void MainGroup_CheckedChanged(object sender, EventArgs e) {
            // Enable/disable the style group and the low/high/bank radio group.
            // Update preview window.
            UpdateControls();
        }

        /// <summary>
        /// Handles Checked event for radio buttons in the Display group.
        /// group box.
        /// </summary>
        private void SimpleDisplay_CheckedChanged(object sender, EventArgs e) {
            // Enable/disable the low/high/bank radio group.
            UpdateControls();
        }

        private void SymbolEntryTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            // Make sure Symbol is checked if they're typing text in.
            //Debug.Assert(radioSimpleDataSymbolic.IsEnabled);
            radioSimpleDataSymbolic.IsChecked = true;
            // Update OK button based on symbol validity.
            UpdateControls();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            CreateDescriptorListFromControls();
            FormatDescriptor.DebugDumpSortedList(Results);
            DialogResult = true;
        }

        /// <summary>
        /// Updates all of the controls to reflect the current internal state.
        /// </summary>
        private void UpdateControls() {
            if (!IsLoaded) {
                return;
            }

            // Configure the simple data "display as" style box.
            bool wantStyle = false;
            int simpleWidth = -1;
            bool isBigEndian = false;
            if (radioSingleBytes.IsChecked == true) {
                wantStyle = true;
                simpleWidth = 1;
            } else if (radio16BitLittle.IsChecked == true) {
                wantStyle = true;
                simpleWidth = 2;
            } else if (radio16BitBig.IsChecked == true) {
                wantStyle = true;
                simpleWidth = 2;
                isBigEndian = true;
            } else if (radio24BitLittle.IsChecked == true) {
                wantStyle = true;
                simpleWidth = 3;
            } else if (radio32BitLittle.IsChecked == true) {
                wantStyle = true;
                simpleWidth = 4;
            }
            bool focusOnSymbol = !simpleDisplayAsGroupBox.IsEnabled && wantStyle;
            simpleDisplayAsGroupBox.IsEnabled = wantStyle;
            if (wantStyle) {
                radioSimpleDataAscii.IsEnabled = IsRawAsciiCompatible(simpleWidth, isBigEndian);
            }

            // Enable the symbolic reference entry box if the "display as" group is enabled.
            // That way instead of "click 16-bit", "click symbol", "enter symbol", the user
            // can skip the second step.
            symbolEntryTextBox.IsEnabled = simpleDisplayAsGroupBox.IsEnabled;

            // Part panel is enabled when Symbol is checked.  (Now handled in XAML.)
            //symbolPartPanel.IsEnabled = (radioSimpleDataSymbolic.IsChecked == true);

            // If we just enabled the group box, set the focus on the symbol entry box.  This
            // removes another click from the steps, though it's a bit aggressive if you're
            // trying to arrow your way through the items.
            if (focusOnSymbol) {
                symbolEntryTextBox.Focus();
            }

            bool isOk = true;
            if (radioSimpleDataSymbolic.IsChecked == true) {
                // Just check for correct format.  References to non-existent labels are allowed.
                isOk = Asm65.Label.ValidateLabel(symbolEntryTextBox.Text);

                // Actually, let's discourage references to auto-labels.
                if (isOk && mSymbolTable.TryGetValue(symbolEntryTextBox.Text, out Symbol sym)) {
                    isOk = sym.SymbolSource != Symbol.Source.Auto;
                }
            }
            IsValid = isOk;
        }

        #region Setup

        /// <summary>
        /// Analyzes the selection to see which data formatting options are suitable.
        /// Disables radio buttons and updates labels.
        /// 
        /// Call this once, when the dialog is first loaded.
        /// </summary>
        private void AnalyzeRanges() {
            Debug.Assert(mSelection.Count != 0);

            string fmt, infoStr;
            if (mSelection.RangeCount == 1 && mSelection.Count == 1) {
                infoStr = (string)FindResource("str_SingleByte");
            } else if (mSelection.RangeCount == 1) {
                fmt = (string)FindResource("str_SingleGroup");
                infoStr = string.Format(fmt, mSelection.Count);
            } else {
                fmt = (string)FindResource("str_MultiGroup");
                infoStr = string.Format(fmt, mSelection.Count, mSelection.RangeCount);
            }
            selectFormatLabel.Text = infoStr;

            IEnumerator<TypedRangeSet.TypedRange> iter = mSelection.RangeListIterator;

            int mixedAsciiOkCount = 0;
            int mixedAsciiNotCount = 0;
            int nullTermStringCount = 0;
            int len8StringCount = 0;
            int len16StringCount = 0;
            int dciStringCount = 0;

            // For each range, check to see if the data within qualifies for the various
            // options.  If any of them fail to meet the criteria, the option is disabled
            // for all ranges.
            while (iter.MoveNext()) {
                TypedRangeSet.TypedRange rng = iter.Current;
                Debug.WriteLine("Testing [" + rng.Low + ", " + rng.High + "]");

                // Start with the easy ones.  Single-byte and dense are always enabled.

                int count = rng.High - rng.Low + 1;
                Debug.Assert(count > 0);
                if ((count & 0x01) != 0) {
                    // not divisible by 2, disallow 16-bit entries
                    radio16BitLittle.IsEnabled = false;
                    radio16BitBig.IsEnabled = false;
                }
                if ((count & 0x03) != 0) {
                    // not divisible by 4, disallow 32-bit entries
                    radio32BitLittle.IsEnabled = false;
                }
                if ((count / 3) * 3 != count) {
                    // not divisible by 3, disallow 24-bit entries
                    radio24BitLittle.IsEnabled = false;
                }


                // Check for run of bytes (2 or more of the same thing).  Remember that
                // we check this one region at a time, and each region could have different
                // bytes, but so long as the bytes are all the same within a region we're good.
                if (radioFill.IsEnabled && count > 1 &&
                        DataAnalysis.RecognizeRun(mFileData, rng.Low, rng.High) == count) {
                    // LGTM
                } else {
                    radioFill.IsEnabled = false;
                }

                // See if there's enough string data to make it worthwhile.  We use an
                // arbitrary threshold of 2+ ASCII characters, and require twice as many
                // ASCII as non-ASCII.  We arbitrarily require the strings to be either
                // high or low ASCII, and treat the other as non-ASCII.  (We could relax
                // this -- we generate separate items for each string and non-ASCII chunk --
                // but I'm trying to hide the option when the buffer doesn't really seem
                // to be holding strings.  Could replace with some sort of minimum string
                // length requirement?)
                if (radioStringMixed.IsEnabled) {
                    int asciiCount;
                    DataAnalysis.CountAsciiBytes(mFileData, rng.Low, rng.High,
                        out int lowAscii, out int highAscii, out int nonAscii);
                    if (highAscii > lowAscii) {
                        asciiCount = highAscii;
                        nonAscii += lowAscii;
                    } else {
                        asciiCount = lowAscii;
                        nonAscii += highAscii;
                    }

                    if (asciiCount >= 2 && asciiCount >= nonAscii * 2) {
                        // Looks good
                        mixedAsciiOkCount += asciiCount;
                        mixedAsciiNotCount += nonAscii;
                    } else {
                        // Fail
                        radioStringMixed.IsEnabled = false;
                        radioStringMixedReverse.IsEnabled = false;
                        mixedAsciiOkCount = mixedAsciiNotCount = -1;
                    }
                }

                // Check for null-terminated strings.  Zero-length strings are allowed, but
                // not counted -- we want to have some actual character data.  Individual
                // strings need to be entirely high-ASCII or low-ASCII, but not all strings
                // in a region have to be the same.
                if (radioStringNullTerm.IsEnabled) {
                    int strCount = DataAnalysis.RecognizeNullTerminatedStrings(mFileData,
                        rng.Low, rng.High);
                    if (strCount > 0) {
                        nullTermStringCount += strCount;
                    } else {
                        radioStringNullTerm.IsEnabled = false;
                        nullTermStringCount = -1;
                    }
                }

                // Check for strings prefixed with an 8-bit length.
                if (radioStringLen8.IsEnabled) {
                    int strCount = DataAnalysis.RecognizeLen8Strings(mFileData, rng.Low, rng.High);
                    if (strCount > 0) {
                        len8StringCount += strCount;
                    } else {
                        radioStringLen8.IsEnabled = false;
                        len8StringCount = -1;
                    }
                }

                // Check for strings prefixed with a 16-bit length.
                if (radioStringLen16.IsEnabled) {
                    int strCount = DataAnalysis.RecognizeLen16Strings(mFileData, rng.Low, rng.High);
                    if (strCount > 0) {
                        len16StringCount += strCount;
                    } else {
                        radioStringLen16.IsEnabled = false;
                        len16StringCount = -1;
                    }
                }

                // Check for DCI strings.  All strings within a single range must have the
                // same "polarity", e.g. low ASCII terminated by high ASCII.
                if (radioStringDci.IsEnabled) {
                    int strCount = DataAnalysis.RecognizeDciStrings(mFileData, rng.Low, rng.High);
                    if (strCount > 0) {
                        dciStringCount += strCount;
                    } else {
                        radioStringDci.IsEnabled = false;
                        dciStringCount = -1;
                    }
                }
            }

            // Update the dialog with string and character counts, summed across all regions.

            const string UNSUP_STR = "xx";
            fmt = (string)FindResource("str_StringMixed");
            string revfmt = (string)FindResource("str_StringMixedReverse");
            if (mixedAsciiOkCount > 0) {
                Debug.Assert(radioStringMixed.IsEnabled);
                radioStringMixed.Content = string.Format(fmt,
                    mixedAsciiOkCount, mixedAsciiNotCount);
                radioStringMixedReverse.Content = string.Format(revfmt,
                    mixedAsciiOkCount, mixedAsciiNotCount);
            } else {
                Debug.Assert(!radioStringMixed.IsEnabled);
                radioStringMixed.Content = string.Format(fmt, UNSUP_STR, UNSUP_STR);
                radioStringMixedReverse.Content = string.Format(revfmt, UNSUP_STR, UNSUP_STR);
            }

            fmt = (string)FindResource("str_StringNullTerm");
            if (nullTermStringCount > 0) {
                Debug.Assert(radioStringNullTerm.IsEnabled);
                radioStringNullTerm.Content = string.Format(fmt, nullTermStringCount);
            } else {
                Debug.Assert(!radioStringNullTerm.IsEnabled);
                radioStringNullTerm.Content = string.Format(fmt, UNSUP_STR);
            }

            fmt = (string)FindResource("str_StringLen8");
            if (len8StringCount > 0) {
                Debug.Assert(radioStringLen8.IsEnabled);
                radioStringLen8.Content = string.Format(fmt, len8StringCount);
            } else {
                Debug.Assert(!radioStringLen8.IsEnabled);
                radioStringLen8.Content = string.Format(fmt, UNSUP_STR);
            }

            fmt = (string)FindResource("str_StringLen16");
            if (len16StringCount > 0) {
                Debug.Assert(radioStringLen16.IsEnabled);
                radioStringLen16.Content = string.Format(fmt, len16StringCount);
            } else {
                Debug.Assert(!radioStringLen16.IsEnabled);
                radioStringLen16.Content = string.Format(fmt, UNSUP_STR);
            }

            fmt = (string)FindResource("str_StringDci");
            if (dciStringCount > 0) {
                Debug.Assert(radioStringDci.IsEnabled);
                radioStringDci.Content = string.Format(fmt, dciStringCount);
            } else {
                Debug.Assert(!radioStringDci.IsEnabled);
                radioStringDci.Content = string.Format(fmt, UNSUP_STR);
            }
        }

        /// <summary>
        /// Determines whether the data in the buffer can be represented as ASCII values.
        /// Using ".DD1 'A'" for 0x41 is obvious, but we also allow ".DD2 'A'" for
        /// 0x41 0x00.  16-bit character constants are more likely as intermediate
        /// operands, but could be found in data areas.
        /// 
        /// High and low ASCII are allowed, and may be freely mixed.
        /// 
        /// Testing explicitly is probably excessive, and possibly counter-productive if
        /// the user is trying to flag an area that is a mix of ASCII and non-ASCII and
        /// just wants hex for the rest, but we'll give it a try.
        /// </summary>
        /// <param name="wordWidth">Number of bytes per character.</param>
        /// <param name="isBigEndian">Word endian-ness.</param>
        /// <returns>True if data in all regions can be represented as high or low ASCII.</returns>
        private bool IsRawAsciiCompatible(int wordWidth, bool isBigEndian) {
            IEnumerator<TypedRangeSet.TypedRange> iter = mSelection.RangeListIterator;
            while (iter.MoveNext()) {
                TypedRangeSet.TypedRange rng = iter.Current;
                Debug.Assert(((rng.High - rng.Low + 1) / wordWidth) * wordWidth ==
                    rng.High - rng.Low + 1);
                for (int i = rng.Low; i <= rng.High; i += wordWidth) {
                    int val = RawData.GetWord(mFileData, rng.Low, wordWidth, isBigEndian);
                    if (val < 0x20 || (val >= 0x7f && val < 0xa0) || val >= 0xff) {
                        // bad value, fail
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Configures the dialog controls based on the provided format descriptor.  If
        /// the desired options are unavailable, a suitable default is selected instead.
        ///
        /// Call from the Loaded event.
        /// </summary>
        /// <param name="dfd">FormatDescriptor to use.</param>
        private void SetControlsFromDescriptor(FormatDescriptor dfd) {
            radioSimpleDataHex.IsChecked = true;
            radioSymbolPartLow.IsChecked = true;

            if (dfd == null) {
                radioDefaultFormat.IsChecked = true;
                return;
            }

            RadioButton preferredFormat;

            switch (dfd.FormatType) {
                case FormatDescriptor.Type.NumericLE:
                case FormatDescriptor.Type.NumericBE:
                    switch (dfd.Length) {
                        case 1:
                            preferredFormat = radioSingleBytes;
                            break;
                        case 2:
                            preferredFormat =
                                (dfd.FormatType == FormatDescriptor.Type.NumericLE ?
                                    radio16BitLittle : radio16BitBig);
                            break;
                        case 3:
                            preferredFormat = radio24BitLittle;
                            break;
                        case 4:
                            preferredFormat = radio32BitLittle;
                            break;
                        default:
                            Debug.Assert(false);
                            preferredFormat = radioDefaultFormat;
                            break;
                    }
                    if (preferredFormat.IsEnabled) {
                        switch (dfd.FormatSubType) {
                            case FormatDescriptor.SubType.None:
                            case FormatDescriptor.SubType.Hex:
                                radioSimpleDataHex.IsChecked = true;
                                break;
                            case FormatDescriptor.SubType.Decimal:
                                radioSimpleDataDecimal.IsChecked = true;
                                break;
                            case FormatDescriptor.SubType.Binary:
                                radioSimpleDataBinary.IsChecked = true;
                                break;
                            case FormatDescriptor.SubType.LowAscii:
                            case FormatDescriptor.SubType.HighAscii:
                            case FormatDescriptor.SubType.C64Petscii:
                            case FormatDescriptor.SubType.C64Screen:
                                // TODO(petscii): update UI
                                radioSimpleDataAscii.IsChecked = true;
                                break;
                            case FormatDescriptor.SubType.Address:
                                radioSimpleDataAddress.IsChecked = true;
                                break;
                            case FormatDescriptor.SubType.Symbol:
                                radioSimpleDataSymbolic.IsChecked = true;
                                switch (dfd.SymbolRef.ValuePart) {
                                    case WeakSymbolRef.Part.Low:
                                        radioSymbolPartLow.IsChecked = true;
                                        break;
                                    case WeakSymbolRef.Part.High:
                                        radioSymbolPartHigh.IsChecked = true;
                                        break;
                                    case WeakSymbolRef.Part.Bank:
                                        radioSymbolPartBank.IsChecked = true;
                                        break;
                                    default:
                                        Debug.Assert(false);
                                        break;
                                }
                                Debug.Assert(dfd.HasSymbol);
                                symbolEntryTextBox.Text = dfd.SymbolRef.Label;
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    } else {
                        // preferred format not enabled; leave Hex/Low checked
                    }
                    break;
                case FormatDescriptor.Type.StringGeneric:
                    preferredFormat = radioStringMixed;
                    break;
                case FormatDescriptor.Type.StringReverse:
                    preferredFormat = radioStringMixedReverse;
                    break;
                case FormatDescriptor.Type.StringNullTerm:
                    preferredFormat = radioStringNullTerm;
                    break;
                case FormatDescriptor.Type.StringL8:
                    preferredFormat = radioStringLen8;
                    break;
                case FormatDescriptor.Type.StringL16:
                    preferredFormat = radioStringLen16;
                    break;
                case FormatDescriptor.Type.StringDci:
                    preferredFormat = radioStringDci;
                    break;
                case FormatDescriptor.Type.Dense:
                    preferredFormat = radioDenseHex;
                    break;
                case FormatDescriptor.Type.Fill:
                    preferredFormat = radioFill;
                    break;
                default:
                    // Should not be here.
                    Debug.Assert(false);
                    preferredFormat = radioDefaultFormat;
                    break;
            }

            if (preferredFormat.IsEnabled) {
                preferredFormat.IsChecked = true;
            } else {
                mPreferredFormatUnavailable = true;
                radioDefaultFormat.IsChecked = true;
            }
        }

        #endregion Setup

        #region FormatDescriptor creation

        /// <summary>
        /// Creates a list of FormatDescriptors, based on the current control configuration.
        /// 
        /// The entries in the list are guaranteed to be sorted by start address and not
        /// overlap.
        /// 
        /// We assume that whatever the control gives us is correct, e.g. it's not going
        /// to tell us to put a buffer full of zeroes into a DCI string.
        /// </summary>
        /// <returns>Result list.</returns>
        private void CreateDescriptorListFromControls() {
            FormatDescriptor.Type type = FormatDescriptor.Type.Default;
            FormatDescriptor.SubType subType = FormatDescriptor.SubType.None;
            WeakSymbolRef symbolRef = null;
            int chunkLength = -1;

            // Decode the "display as" panel, if it's relevant.
            if (radioSimpleDataHex.IsEnabled) {
                if (radioSimpleDataHex.IsChecked == true) {
                    subType = FormatDescriptor.SubType.Hex;
                } else if (radioSimpleDataDecimal.IsChecked == true) {
                    subType = FormatDescriptor.SubType.Decimal;
                } else if (radioSimpleDataBinary.IsChecked == true) {
                    subType = FormatDescriptor.SubType.Binary;
                } else if (radioSimpleDataAscii.IsChecked == true) {
                    // TODO(petscii): add PETSCII buttons
                    subType = FormatDescriptor.SubType.ASCII_GENERIC;
                } else if (radioSimpleDataAddress.IsChecked == true) {
                    subType = FormatDescriptor.SubType.Address;
                } else if (radioSimpleDataSymbolic.IsChecked == true) {
                    WeakSymbolRef.Part part;
                    if (radioSymbolPartLow.IsChecked == true) {
                        part = WeakSymbolRef.Part.Low;
                    } else if (radioSymbolPartHigh.IsChecked == true) {
                        part = WeakSymbolRef.Part.High;
                    } else if (radioSymbolPartBank.IsChecked == true) {
                        part = WeakSymbolRef.Part.Bank;
                    } else {
                        Debug.Assert(false);
                        part = WeakSymbolRef.Part.Low;
                    }
                    subType = FormatDescriptor.SubType.Symbol;
                    symbolRef = new WeakSymbolRef(symbolEntryTextBox.Text, part);
                } else {
                    Debug.Assert(false);
                }
            } else {
                subType = 0;        // set later, or doesn't matter
            }

            // Decode the main format.
            if (radioDefaultFormat.IsChecked == true) {
                // Default/None; note this would create a multi-byte Default format, which isn't
                // really allowed.  What we actually want to do is remove the explicit formatting
                // from all spanned offsets, so we use a dedicated type for that.
                type = FormatDescriptor.Type.REMOVE;
            } else if (radioSingleBytes.IsChecked == true) {
                type = FormatDescriptor.Type.NumericLE;
                chunkLength = 1;
            } else if (radio16BitLittle.IsChecked == true) {
                type = FormatDescriptor.Type.NumericLE;
                chunkLength = 2;
            } else if (radio16BitBig.IsChecked == true) {
                type = FormatDescriptor.Type.NumericBE;
                chunkLength = 2;
            } else if (radio24BitLittle.IsChecked == true) {
                type = FormatDescriptor.Type.NumericLE;
                chunkLength = 3;
            } else if (radio32BitLittle.IsChecked == true) {
                type = FormatDescriptor.Type.NumericLE;
                chunkLength = 4;
            } else if (radioDenseHex.IsChecked == true) {
                type = FormatDescriptor.Type.Dense;
            } else if (radioFill.IsChecked == true) {
                type = FormatDescriptor.Type.Fill;
            } else if (radioStringMixed.IsChecked == true) {
                // TODO(petscii): encoding format will come from a combo box; that determines
                //   the subType and the arg to the string-creation functions, which use the
                //   appropriate char encoding methods to break up the strings
                type = FormatDescriptor.Type.StringGeneric;
                subType = FormatDescriptor.SubType.LowAscii;
            } else if (radioStringMixedReverse.IsChecked == true) {
                type = FormatDescriptor.Type.StringReverse;
                subType = FormatDescriptor.SubType.LowAscii;
            } else if (radioStringNullTerm.IsChecked == true) {
                type = FormatDescriptor.Type.StringNullTerm;
                subType = FormatDescriptor.SubType.LowAscii;
            } else if (radioStringLen8.IsChecked == true) {
                type = FormatDescriptor.Type.StringL8;
                subType = FormatDescriptor.SubType.LowAscii;
            } else if (radioStringLen16.IsChecked == true) {
                type = FormatDescriptor.Type.StringL16;
                subType = FormatDescriptor.SubType.LowAscii;
            } else if (radioStringDci.IsChecked == true) {
                type = FormatDescriptor.Type.StringDci;
                subType = FormatDescriptor.SubType.LowAscii;
            } else {
                Debug.Assert(false);
                // default/none
            }


            Results = new SortedList<int, FormatDescriptor>();

            IEnumerator<TypedRangeSet.TypedRange> iter = mSelection.RangeListIterator;
            while (iter.MoveNext()) {
                TypedRangeSet.TypedRange rng = iter.Current;

                // TODO(petscii): handle encoding on all four calls
                switch (type) {
                    case FormatDescriptor.Type.StringGeneric:
                    case FormatDescriptor.Type.StringReverse:
                        CreateMixedStringEntries(rng.Low, rng.High, type, subType);
                        break;
                    case FormatDescriptor.Type.StringNullTerm:
                        CreateCStringEntries(rng.Low, rng.High, type, subType);
                        break;
                    case FormatDescriptor.Type.StringL8:
                    case FormatDescriptor.Type.StringL16:
                        CreateLengthStringEntries(rng.Low, rng.High, type, subType);
                        break;
                    case FormatDescriptor.Type.StringDci:
                        CreateDciStringEntries(rng.Low, rng.High, type, subType);
                        break;
                    default:
                        CreateSimpleEntries(type, subType, chunkLength, symbolRef,
                            rng.Low, rng.High);
                        break;
                }
            }
        }

        /// <summary>
        /// Creates one or more FormatDescriptor entries for the specified range, adding them
        /// to the Results list.
        /// 
        /// This will either create one entry that spans the entire range (for e.g. strings
        /// and bulk data), or create equal-sized chunks.
        /// </summary>
        /// <param name="type">Region data type.</param>
        /// <param name="subType">Region data sub-type.</param>
        /// <param name="chunkLength">Length of a chunk, or -1 for full buffer.</param>
        /// <param name="symbolRef">Symbol reference, or null if not applicable.</param>
        /// <param name="low">Offset of first byte in range.</param>
        /// <param name="high">Offset of last byte in range.</param>
        private void CreateSimpleEntries(FormatDescriptor.Type type,
                FormatDescriptor.SubType subType, int chunkLength,
                WeakSymbolRef symbolRef, int low, int high) {

            if (chunkLength == -1) {
                chunkLength = (high - low) + 1;
            }
            Debug.Assert(((high - low + 1) / chunkLength) * chunkLength == high - low + 1);

            // Either we have one chunk, or we have multiple chunks with the same type and
            // length.  Either way, we only need to create the descriptor once.  (This is
            // safe because FormatDescriptor instances are immutable.)
            //
            // The one exception to this is ASCII values for non-string data, because we have
            // to dig the low vs. high value out of the data itself.
            FormatDescriptor dfd;
            if (subType == FormatDescriptor.SubType.Symbol) {
                dfd = FormatDescriptor.Create(chunkLength, symbolRef,
                    type == FormatDescriptor.Type.NumericBE);
            } else {
                dfd = FormatDescriptor.Create(chunkLength, type, subType);
            }
            while (low <= high) {
                if (subType == FormatDescriptor.SubType.ASCII_GENERIC) {
                    Debug.Assert(dfd.IsNumeric);
                    int val = RawData.GetWord(mFileData, low, dfd.Length,
                        type == FormatDescriptor.Type.NumericBE);
                    FormatDescriptor.SubType actualSubType = (val > 0x7f) ?
                        FormatDescriptor.SubType.HighAscii : FormatDescriptor.SubType.LowAscii;
                    if (actualSubType != dfd.FormatSubType) {
                        // replace the descriptor
                        dfd = FormatDescriptor.Create(chunkLength, type, actualSubType);
                    }
                }

                Results.Add(low, dfd);
                low += chunkLength;
            }
        }

        /// <summary>
        /// Creates one or more FormatDescriptor entries for the specified range, adding them
        /// to the Results list.
        /// </summary>
        /// <param name="low">Offset of first byte in range.</param>
        /// <param name="high">Offset of last byte in range.</param>
        /// <param name="subType">String sub-type.</param>
        private void CreateMixedStringEntries(int low, int high, FormatDescriptor.Type type,
                FormatDescriptor.SubType subType) {
            int stringStart = -1;
            int highBit = 0;
            int cur;
            for (cur = low; cur <= high; cur++) {
                byte val = mFileData[cur];
                if (CommonUtil.TextUtil.IsHiLoAscii(val)) {
                    // is ASCII
                    if (stringStart >= 0) {
                        // was in a string
                        if (highBit != (val & 0x80)) {
                            // end of string due to high bit flip, output
                            CreateStringOrByte(stringStart, cur - stringStart, subType);
                            // start a new string
                            stringStart = cur;
                        } else {
                            // still in string, keep going
                        }
                    } else {
                        // wasn't in a string, start one
                        stringStart = cur;
                    }
                    highBit = val & 0x80;
                } else {
                    // not ASCII
                    if (stringStart >= 0) {
                        // was in a string, output it
                        CreateStringOrByte(stringStart, cur - stringStart, subType);
                        stringStart = -1;
                    }
                    // output as single byte
                    CreateByteFD(cur, FormatDescriptor.SubType.Hex);
                }
            }
            if (stringStart >= 0) {
                // close out the string
                CreateStringOrByte(stringStart, cur - stringStart, subType);
            }
        }

        /// <summary>
        /// Creates a format descriptor for ASCII data.  If the data is only one byte long,
        /// a single-byte ASCII char item is emitted instead.
        /// </summary>
        /// <param name="offset">Offset of first byte.</param>
        /// <param name="length">Length of string.</param>
        /// <param name="subType">String sub-type.</param>
        private void CreateStringOrByte(int offset, int length, FormatDescriptor.SubType subType) {
            Debug.Assert(length > 0);
            if (length == 1) {
                // Single byte, output as single char rather than 1-byte string.  We use the
                // same encoding as the rest of the string.
                CreateByteFD(offset, subType);
            } else {
                FormatDescriptor dfd;
                dfd = FormatDescriptor.Create(length,
                    FormatDescriptor.Type.StringGeneric, subType);
                Results.Add(offset, dfd);
            }
        }

        /// <summary>
        /// Creates a format descriptor for a single-byte numeric value.
        /// </summary>
        /// <param name="offset">File offset.</param>
        /// <param name="subType">How to format the item.</param>
        private void CreateByteFD(int offset, FormatDescriptor.SubType subType) {
            FormatDescriptor dfd = FormatDescriptor.Create(1,
                FormatDescriptor.Type.NumericLE, subType);
            Results.Add(offset, dfd);
        }

        /// <summary>
        /// Creates one or more FormatDescriptor entries for the specified range, adding them
        /// to the Results list.
        /// </summary>
        /// <param name="low">Offset of first byte in range.</param>
        /// <param name="high">Offset of last byte in range.</param>
        /// <param name="subType">String sub-type.</param>
        private void CreateCStringEntries(int low, int high, FormatDescriptor.Type type,
                FormatDescriptor.SubType subType) {
            int startOffset = low;
            for (int i = low; i <= high; i++) {
                if (mFileData[i] == 0x00) {
                    // End of string.  Zero-length strings are allowed.
                    FormatDescriptor dfd = FormatDescriptor.Create(
                        i - startOffset + 1, type, subType);
                    Results.Add(startOffset, dfd);
                    startOffset = i + 1;
                } else {
                    // keep going
                }
            }

            // Earlier analysis guaranteed that the last byte in the buffer is 0x00.
            Debug.Assert(startOffset == high + 1);
        }

        /// <summary>
        /// Creates one or more FormatDescriptor entries for the specified range, adding them
        /// to the Results list.
        /// </summary>
        /// <param name="low">Offset of first byte in range.</param>
        /// <param name="high">Offset of last byte in range.</param>
        /// <param name="subType">String sub-type.</param>
        private void CreateLengthStringEntries(int low, int high, FormatDescriptor.Type type,
                FormatDescriptor.SubType subType) {
            int i;
            for (i = low; i <= high;) {
                int length = mFileData[i];
                if (type == FormatDescriptor.Type.StringL16) {
                    length |= mFileData[i + 1] << 8;
                    length += 2;
                } else {
                    length++;
                }
                // Zero-length strings are allowed.
                FormatDescriptor dfd = FormatDescriptor.Create(length, type, subType);
                Results.Add(i, dfd);
                i += length;
            }

            Debug.Assert(i == high + 1);
        }

        /// <summary>
        /// Creates one or more FormatDescriptor entries for the specified range, adding them
        /// to the Results list.
        /// </summary>
        /// <param name="low">Offset of first byte in range.</param>
        /// <param name="high">Offset of last byte in range.</param>
        /// <param name="subType">String sub-type.</param>
        private void CreateDciStringEntries(int low, int high, FormatDescriptor.Type type,
                FormatDescriptor.SubType subType) {
            int end, endMask;

            end = high + 1;

            // Zero-length strings aren't a thing for DCI.  The analyzer requires that all
            // strings in a region have the same polarity, so just grab the last byte.
            endMask = mFileData[end - 1] & 0x80;

            int stringStart = low;
            for (int i = low; i != end; i++) {
                byte val = mFileData[i];
                if ((val & 0x80) == endMask) {
                    // found the end of a string
                    int length = (i - stringStart) + 1;
                    FormatDescriptor dfd = FormatDescriptor.Create(length, type, subType);
                    Results.Add(stringStart < i ? stringStart : i, dfd);
                    stringStart = i + 1;
                }
            }

            Debug.Assert(stringStart == end);
        }

        #endregion FormatDescriptor creation
    }
}
