﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using NLog;
using System;
using System.Text.RegularExpressions;

namespace Butterfly.Core.Util.Field {
    public class PhoneFieldValidator : IFieldValidator {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected readonly string name;
        protected readonly bool allowNull;

        protected static readonly Regex NON_PHONE_CHARS = new Regex(@"[^\+0-9]");

        public PhoneFieldValidator(string name, bool allowNull = true) {
            this.name = name;
            this.allowNull = allowNull;
        }

        public string Validate(string value) {
            logger.Debug($"Validate():value={value}");

            if (string.IsNullOrEmpty(value)) {
                if (this.allowNull) return value;
                throw new Exception($"Field {this.name} cannot be null");
            }

            string newPhone = NON_PHONE_CHARS.Replace(value, "").Trim();
            if (!newPhone.StartsWith("+") && newPhone.Length == 10) {
                return $"+1{newPhone}";
            }
            else {
                return newPhone;
            }
        }
    }
}
