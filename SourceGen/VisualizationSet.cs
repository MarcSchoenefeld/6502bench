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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceGen {
    public class VisualizationSet {
        // TODO(xyzzy)
        public string PlaceHolder { get; private set; }

        public VisualizationSet(string placeHolder) {
            PlaceHolder = placeHolder;
        }


        public override string ToString() {
            return "[VS: " + PlaceHolder + "]";
        }

        public static bool operator ==(VisualizationSet a, VisualizationSet b) {
            if (ReferenceEquals(a, b)) {
                return true;    // same object, or both null
            }
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return false;   // one is null
            }
            // All fields must be equal.
            return a.PlaceHolder == b.PlaceHolder;
        }
        public static bool operator !=(VisualizationSet a, VisualizationSet b) {
            return !(a == b);
        }
        public override bool Equals(object obj) {
            return obj is VisualizationSet && this == (VisualizationSet)obj;
        }
        public override int GetHashCode() {
            return PlaceHolder.GetHashCode();
        }
    }
}
