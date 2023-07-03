import React, {useState} from 'react';
import {render} from 'react-dom';

class LoadedPage {
    constructor(public readonly statistics: Statistics, public readonly messages: Message[]) {}
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

const DefaultLimit = 100;

const getPage = async (offset: number): Promise<LoadedPage> => {
    const statistics = await getStatistics();
    const messages = await getMessages(offset, DefaultLimit);
    return new LoadedPage(statistics, messages);
}

const App = () => {
    const [state, setState] = useState<State>('Loading');
    if (state === 'Loading') {
        getPage(0)
            .then(page => setState(page))
            .catch(error => setState(new ErrorState(error.message)));
    }

    if (state === 'Loading') {
        return <div className="loading">Loading…</div>
    } else if (state instanceof ErrorState) {
        return <div className="error">Error: {state.error}</div>
    } else {
        // TODO: message icon
        return <div className="page">
            Count: {state.statistics.messageCount}
            <table>
                <tr><th>Date</th><th>Author</th><th>Message</th></tr>
                {state.messages.map(message => <tr>
                    <td>{message.dateTime}</td>
                    <td>{message.sender}</td>
                    <td>{message.text}</td>
                </tr>)}
            </table>
        </div>
    }
};

render(<App/>, document.getElementById('app'));
