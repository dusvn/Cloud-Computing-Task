# Cloud-Computing-Task
# Appoarch of solution
Main problem of this task is to have consistent state in reliable collection of user and reliable collection of books.<br/> 
This appoarch is 2 phase commit and this works for small amount of data.<br/> 
Solution for huge systems is <b>SAGA PATTERN<b/> .<br/> 
Break down to this solution and how it's works.<br/> 
1.Client WEB API pick up data from front and send it to validator<br>
2.Validator do some basic validation <br/> 
3.Transaction coordinator make process of 2 phase commit <br/> 
  3.1.First Phase<br/> 
  -Contact Bank and BookStore microservices to be sure user have correct amount of money,for books and also contact book store to be sure about number of available books at this moment.<br/>
  -There is tiny trick to make consistent state in our small system.We have one dictionary for books and also one for users to make lock on this item until purchase process ends.<br>
  -After transaction sytem will unlock item for other users.<br>
  -In prepare method system check is all conditions satisfied and if not then system do roll back.<br/>
  -Why we need to implement this in our system?<br/>
  -Service fabric provides to make more independent calls of functions but greath to know is service fabric don't take care about our data.Like classic platform as as service we need to take care about data and how it will bi processed.<br/>
  3.2 Second Phase<br/>
  -Commit<br/>
  -Take money from user for this amount of books and also change quantity of book store.<br/><br/> 

Why we actualy need SAGA PATTERN for huge systems? <br/> 
This is becouse this lock operation will make system very slow and there SAGA PATTERN help us to solve this.<br/> 
Front App-React 
Client-Stateless microservice web api .net core <br/> 
Validator-Stateless microservice .net core <br/> 
Transaction Coordinator-Stateless microservice .net core<br/>
Book Store - Statefull microservice .net core
Bank - Statefull microservice .net core 
![image](https://github.com/user-attachments/assets/b5384456-3712-4e2b-ba78-8128b2c79bfb)
