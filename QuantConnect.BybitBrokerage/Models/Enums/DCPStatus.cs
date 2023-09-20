﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System.Runtime.Serialization;

namespace QuantConnect.BybitBrokerage.Models.Enums;

/// <summary>
/// Disconnect cancel all status
/// <seealso href="https://bybit-exchange.github.io/docs/v5/order/dcp"/>
/// </summary>
public enum DCPStatus
{
    /// <summary>
    /// Orders will stay open on disconnect
    /// </summary>
    [EnumMember(Value = "OFF")] Off,
    
    /// <summary>
    /// All open orders will be closed on disconnect
    /// </summary>
    [EnumMember(Value = "ON")] On
}