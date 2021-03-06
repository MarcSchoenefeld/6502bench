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

using CommonUtil;

namespace PluginCommon {
    /// <summary>
    /// Read-only wrapper around AddressMap.
    /// 
    /// Instance is immutable, though in theory the underlying AddressMap could change if
    /// some other code has a reference to it.
    /// </summary>
    /// <remarks>
    /// This is currently simple enough that it could just be an interface, but I don't want
    /// to rely on that remaining true.
    /// </remarks>
    public class AddressTranslate {
        private AddressMap mAddrMap;

        public AddressTranslate(AddressMap addrMap) {
            mAddrMap = addrMap;
        }

        /// <summary>
        /// Determines the file offset that best contains the specified target address.
        /// </summary>
        /// <param name="srcOffset">Offset of the address reference.  Only matters when
        ///   multiple file offsets map to the same address.</param>
        /// <param name="targetAddr">Address to look up.</param>
        /// <returns>The file offset, or -1 if the address falls outside the file.</returns>
        public int AddressToOffset(int srcOffset, int targetAddr) {
            return mAddrMap.AddressToOffset(srcOffset, targetAddr);
        }

        /// <summary>
        /// Converts a file offset to an address.
        /// </summary>
        /// <param name="offset">File offset.</param>
        /// <returns>24-bit address.</returns>
        public int OffsetToAddress(int offset) {
            return mAddrMap.OffsetToAddress(offset);
        }
    }
}
