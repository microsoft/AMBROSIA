import ts from "typescript"; // For TypeScript AST parsing
import fs = require("fs");
import Path = require("path");
import Process = require("process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Utils from "../src/Utils/Utils-Index";

/** Namespace for abstract syntax tree parsing of TypeScript files. */
export namespace AST
{
    let _syntaxKindNames: { [kind: number]: string } = {}; // Mapping from SyntaxKind value to SyntaxKind name
    let _typeChecker: ts.TypeChecker = null;

    export function printAST(fileName: string): void
    {
        Utils.log("Current directory is "+ Process.cwd());

        // Check that the file exists
        if (!fs.existsSync(fileName))
        {
            let fullFileName: string = Path.resolve(fileName);
            throw new Error("The file specified ('" + fullFileName + "') does not exist");
        }

        let program: ts.Program = ts.createProgram([fileName], { removeComments: false });
        let sourceFile: ts.SourceFile = program.getSourceFile(fileName);
        _typeChecker = program.getTypeChecker();

        // let sourceText: string = sourceFile.getText();
        // walkAST(sourceFile, 0);
        walkFullAST(sourceFile, 0, sourceFile);
    }

    // This version returns a limited set of nodes
    function walkAST(nodeToWalk: ts.Node, depth: number)
    {
        ts.forEachChild(nodeToWalk, node =>
        {
            logNodeName(node, depth);
            walkAST(node, depth + 2);
        });
    }

    // This version returns ALL nodes (including those for keywords, punctuation, and - critically - JSDoc)
    // Note: You can verify the output using https://ts-ast-viewer.com/#
    function walkFullAST(nodeToWalk: ts.Node, depth: number, sourceFile: ts.SourceFile)
    {
        let nodes: ts.Node[] = nodeToWalk.getChildren(sourceFile);
        nodes.forEach(node =>
        {
            logNodeName(node, depth);
            walkFullAST(node, depth + 2, sourceFile);
        });
    }

    // [See https://github.com/dsherret/ts-ast-viewer/blob/master/src/components/PropertiesViewer.tsx]
    function logNodeName(node: ts.Node, depth: number)
    {
        let name: string = "";
        let symbol: ts.Symbol = (node as any).symbol as ts.Symbol || _typeChecker.getSymbolAtLocation(node);

        if (symbol)
        {
            name = " (" + symbol.getName() + ")";
        }

        if (node.kind === ts.SyntaxKind.JSDocTag)
        {
            let tag: ts.JSDocTag = node as ts.JSDocTag;
            name = " (@" + tag.tagName.text + (tag.comment ? " " + tag.comment.trim() : "") + ")";
        }
    
        Utils.log(" ".repeat(depth) + getSyntaxKindName(node.kind) + name);
    }

    function getSyntaxKindName(kind: ts.SyntaxKind)
    {
        // Populate _syntaxKindNames (if needed)
        if (Object.keys(_syntaxKindNames).length === 0)
        {
            // A TypeScript enum object contains keys for BOTH the values (number) and corresponding value name (string).
            // The line below gets only the name (string) keys.
            for (const name of Object.keys(ts.SyntaxKind).filter(key => isNaN(parseInt(key, 10))))
            {
                // Unfortunately, some SyntaxKind values are re-used (eg. SyntaxKind[18] is the value of both "OpenBraceToken" and "FirstPunctuation").
                // So here we're simply taking the first used name and associating it to the value (when we encounter a re-used value it will be ignored).
                // [See https://github.com/dsherret/ts-ast-viewer/blob/master/src/utils/getSyntaxKindName.ts]
                const value: number = ts.SyntaxKind[name];
                if (_syntaxKindNames[value] === undefined)
                {
                    _syntaxKindNames[value] = name;
                }
            }
        }
        return (_syntaxKindNames[kind]);
    }
}