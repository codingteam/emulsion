import React, {useState} from 'react';
import {render} from 'react-dom';

class LoadedPage {
    constructor(
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

const DefaultLimit = 10;

const getPage = async (pageIndex: number): Promise<LoadedPage> => {
    const offset = pageIndex * DefaultLimit;
    const statistics = await getStatistics();
    const messages = await getMessages(offset, DefaultLimit);
    return new LoadedPage(statistics, pageIndex, messages);
}

const loadPage = (index: number, setState: (state: State) => void) => {
    getPage(index)
        .then(page => setState(page))
        .catch(error => setState(new ErrorState(error.message)));
}

const PageControls = ({page, setState}: {page: LoadedPage, setState: (state: State) => void}) => {
    const lastPageNumber= Math.ceil(page.statistics.messageCount / DefaultLimit);
    const lastPageIndex= lastPageNumber - 1;
    return <>
        Count: {page.statistics.messageCount}<br/>
        Page: {page.pageIndex + 1} of {Math.ceil(page.statistics.messageCount / DefaultLimit)}<br/>
        <button onClick={() => loadPage(0, setState)}>⇐</button>
        <button onClick={() => loadPage(page.pageIndex - 1, setState)}>←</button>
        <button onClick={() => loadPage(page.pageIndex + 1, setState)}>→</button>
        <button onClick={() => loadPage(lastPageIndex, setState)}>⇒</button>
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
        loadPage(0, setState)
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
