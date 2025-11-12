import path from 'path';
import { fileURLToPath } from 'url';
import NodePolyfillPlugin from "node-polyfill-webpack-plugin";
import webpack from "webpack";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default {
    entry: {
        assetUsageTracker: './src/AssetUsageTracker.tsx',
    },
    target: ["web", "es2020"],
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: 'ts-loader',
                exclude: /node_modules/,
            },
            {
                test: /\.m?js$/,
                resolve: {
                    fullySpecified: false
                },
            },
            {
                test: /\.css$/,
                use: ['style-loader', 'css-loader'],
            },
        ],
    },
    resolve: {
        extensions: ['.tsx', '.ts', '.js'],
        fallback: {
            fs: false,
            https: false,
        },
    },
    devtool: "source-map",
    output: {
        filename: '[name].js',
        path: path.resolve(__dirname, 'dist'),
        library: {
            type: "module",
        },
    },
    experiments: {
        outputModule: true,
        backCompat: false
    },
    optimization: {
        minimize: true,
    },
    externals: {
        'react': 'React',
        'react-dom': 'ReactDOM'
    },
    plugins: [
        new NodePolyfillPlugin(),
        new webpack.ProvidePlugin({
            process: "process/browser",
        }),
    ]
};