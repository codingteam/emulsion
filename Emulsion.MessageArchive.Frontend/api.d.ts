// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

type Statistics = {
    messageCount: number;
}

type Message = {
    messageSystemId: string;
    sender: string;
    dateTime: string;
    text: string;
}
