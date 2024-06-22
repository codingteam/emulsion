// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

import React, {useState} from 'react';
import {render} from 'react-dom';

class LoadedPage {
    constructor(
        public readonly limit: number,
        public readonly statistics: Statistics,
        public readonly pageIndex: number,
        public readonly messages: Message[]
    ) {}
}

class ErrorState {
    constructor(public readonly error: string) {}
}

type State = 'Loading' | LoadedPage | ErrorState;

const getStatistics = async (): Promise<Statistics> => {
    let url = window.location.href
    url = url.substring(0, url.lastIndexOf('/'));

    let response = await fetch(`${url}/api/history/statistics`);
    return await response.json();
};

const getMessages = async (offset: number, limit: number): Promise<Message[]> => {
    let url = window.location.href
    url = url.substring(0, url.lastIndexOf('/'));

    let response = await fetch(`${url}/api/history/messages?offset=${offset}&limit=${limit}`);
    return await response.json();
}

const getPage = async (pageIndex: number, limit: number): Promise<LoadedPage> => {
    const offset = pageIndex * limit;
    const statistics = await getStatistics();
    const messages = await getMessages(offset, limit);
    return new LoadedPage(limit, statistics, pageIndex, messages);
}

const loadPage = (index: number, limit: number, setState: (state: State) => void) => {
    getPage(index, limit)
        .then(page => setState(page))
        .catch(error => setState(new ErrorState(error.message)));
}

const LimitControl = ({limit, onChange}: {limit: number, onChange: (limit: number) => void}) => {
    const values = [
        25,
        50,
        100,
        500
    ]
    return <select defaultValue={limit} onChange={e => onChange(parseInt(e.target.value))}>
        {values.map(v => <option value={v}>{v}</option>)}
    </select>
};

const PageControls = ({page, setState}: {page: LoadedPage, setState: (state: State) => void}) => {
    const lastPageNumber= Math.ceil(page.statistics.messageCount / page.limit);
    const lastPageIndex= lastPageNumber - 1;
    return <>
        Count: {page.statistics.messageCount}<br/>
        Page: {page.pageIndex + 1} of {Math.ceil(page.statistics.messageCount / page.limit)}<br/>
        Show messages per page: <LimitControl limit={page.limit} onChange={(limit) => loadPage(0, limit, setState)}/><br/>
        <button onClick={() => loadPage(0, page.limit, setState)}>⇐</button>
        <button onClick={() => loadPage(page.pageIndex - 1, page.limit, setState)}>←</button>
        <button onClick={() => loadPage(page.pageIndex + 1, page.limit, setState)}>→</button>
        <button onClick={() => loadPage(lastPageIndex, page.limit, setState)}>⇒</button>
    </>;
}

const dateTimeToText = (dateTime: string) => {
    const fullText = new Date(dateTime).toISOString();
    return fullText.substring(0, fullText.lastIndexOf('.')) + 'Z';
}

const renderMessageText = (text: string) => {
    const lines= text.split('\n');
    return lines.map((line) => <p>{line}</p>);
}

const renderMessage = (message: Message) => <div className="message">
    <div className="datetime">{dateTimeToText(message.dateTime)}</div>
    <div className="sender">{message.sender}</div>
    <div className="text">{renderMessageText(message.text)}</div>
</div>

const MessageList = ({list}: {list: Message[]}) => <div className="message-list">
    {list.map(renderMessage)}
</div>;

const App = () => {
    const [state, setState] = useState<State>('Loading');
    if (state === 'Loading') {
        loadPage(0, 100, setState)
    }

    if (state === 'Loading') {
        return <div className="loading">Loading…</div>
    } else if (state instanceof ErrorState) {
        return <div className="error">Error: {state.error}</div>
    } else {
        return <div className="page">
            <PageControls page={state} setState={setState}/>
            <MessageList list={state.messages}/>
        </div>
    }
};

render(<App/>, document.getElementById('app'));
