import React, {useState} from 'react';
import {render} from 'react-dom';

class LoadedPage {
    constructor(public readonly statistics: Statistics) {}
}

class ErrorState {
    constructor(public readonly error: string) {}
}

type State = 'Loading' | LoadedPage | ErrorState;

const getStatistics = (): Promise<Statistics> => {
    let url = window.location.href
    url = url.substring(0, url.lastIndexOf('/'));

    return fetch(`${url}/api/history/statistics`)
        .then(response => response.json())
};

const App = () => {
    const [state, setState] = useState<State>('Loading');
    if (state === 'Loading') {
        getStatistics()
            .then(statistics => setState(new LoadedPage(statistics)))
            .catch(error => setState(new ErrorState(error.message)));
    }

    if (state === 'Loading') {
        return <div className="loading">Loading…</div>
    } else if (state instanceof ErrorState) {
        return <div className="error">Error: {state.error}</div>
    } else {
        return <div className="page">Count: {state.statistics.messageCount}</div>
    }
};

render(<App/>, document.getElementById('app'));
