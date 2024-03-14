create table customers (
    id int not null,
    name varchar(200) not null,
    address varchar(200)
)   

create table orders (
    id int not null,
    customerId int not null,
    recordedDate date,
    amount money
)