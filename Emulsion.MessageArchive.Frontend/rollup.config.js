import commonjs from '@rollup/plugin-commonjs';
import copy from 'rollup-plugin-copy';
import resolve from '@rollup/plugin-node-resolve';
import typescript from '@rollup/plugin-typescript';

export default {
    input: 'app.tsx',
    output: {
        file: 'bin/app.js',
        format: 'iife',
        sourcemap: true,
        // globals: {
        //     'react': 'React',
        //     'react-dom': 'ReactDOM'
        // }
    },
    plugins: [
        copy({
            targets: [
                {src: 'index.html', dest: 'bin'}
            ]
        }),
        resolve(),
        commonjs(),
        typescript()
    ],
    // external: ['react', 'react-dom']
};
