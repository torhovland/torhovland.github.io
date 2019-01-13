open System

type Person = { 
    FirstName: string;
    LastName: string;
}

let someFunction (requiredParam: Person) (optionalParam: Person option) =
    match optionalParam with
    | None ->
        printfn "Only got one person with names of length %d and %d." 
            requiredParam.FirstName.Length 
            requiredParam.LastName.Length 
    | Some secondPerson ->
        printfn "Got two people, the second having lengths %d and %d." 
            secondPerson.FirstName.Length 
            secondPerson.LastName.Length         

[<EntryPoint>]
let main argv =
    let person = { FirstName = "John"; LastName = None }
    someFunction person None
    0 // return an integer exit code
